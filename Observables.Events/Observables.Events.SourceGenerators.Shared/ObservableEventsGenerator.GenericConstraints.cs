using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Observables.SourceGenerators.Shared;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Observables.SourceGenerators.Shared.Diagnostics;
using Observables.SourceGenerators.Shared.Extensions;

namespace Observables.Events.Generators;

public sealed partial class ObservableEventsGenerator
{
    private static IEnumerable<IEventSymbol> GetGenericConstraintEvents(
        GenericConstraintTarget target,
        Compilation compilation,
        Action<string, Location?, string> reportDiagnostic)
    {
        var byName = new Dictionary<string, IEventSymbol>(System.StringComparer.Ordinal);
        foreach (var constraintType in target.ConstraintTypes)
        {
            foreach (var evt in GetPublicInstanceEventsFromTypeAndBases(constraintType))
            {
                // The accessor casts the sender to evt.ContainingType — the member (and thus its
                // declaring type) must be reachable from the consumer, and the signature must be
                // nameable in generated source.
                if (!compilation.IsSymbolAccessibleWithin(evt, compilation.Assembly)
                    || !IsEventSignatureNameable(evt, compilation))
                {
                    continue;
                }

                if (byName.TryGetValue(evt.Name, out var existing))
                {
                    if (!SymbolEqualityComparer.Default.Equals(existing.Type, evt.Type))
                    {
                        reportDiagnostic(DiagnosticDescriptors.IncompatibleEventMerge.Id, evt.Locations.FirstOrDefault(), evt.Name);
                        continue;
                    }

                    continue;
                }

                byName[evt.Name] = evt;
            }
        }

        return byName.Values.OrderBy(static e => e.Name, System.StringComparer.Ordinal);
    }
    private static string GetConstraintPartName(INamedTypeSymbol t, bool qualified)
    {
        var baseName = t.TypeKind == TypeKind.Interface && t.Name.Length >= 2 && t.Name[0] == 'I' && char.IsUpper(t.Name[1])
            ? t.Name.Substring(1)
            : t.Name;
        if (t.IsGenericType)
            baseName += $"_{t.Arity}";
        if (!qualified)
            return baseName;
        var prefix = TypeIdentityPrefix(t);
        return prefix.Length == 0 ? baseName : $"{prefix}_{baseName}";
    }

    private static string GetGenericConstraintCombinedInterfaceName(
        GenericConstraintTarget target,
        ObservableEventsEntryKind entryKind,
        bool qualified)
    {
        var suffix = entryKind == ObservableEventsEntryKind.Events ? "Events" : "EventHandlers";
        return $"I{string.Join("_", target.ConstraintTypes.Select(t => GetConstraintPartName(t, qualified)))}{suffix}";
    }

    private static string GetGenericConstraintCombinedImplName(
        GenericConstraintTarget target,
        ObservableEventsEntryKind entryKind,
        bool qualified)
    {
        var suffix = entryKind == ObservableEventsEntryKind.Events ? "Events" : "EventHandlers";
        return $"{string.Join("_", target.ConstraintTypes.Select(t => GetConstraintPartName(t, qualified)))}{suffix}Impl";
    }

    /// <summary>
    /// Short constraint-type names collide across namespaces and arity (and can shadow a
    /// hierarchy interface name): compute which unqualified combined names are taken so the
    /// affected targets can re-emit with namespace-qualified parts.
    /// </summary>
    private static (System.Collections.Generic.HashSet<string> InterfaceNames, System.Collections.Generic.HashSet<string> ImplNames)
        CollectCollidingGenericConstraintNames(
            ImmutableArray<GenericConstraintTarget> targets,
            Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
            ObservableEventsEntryKind entryKind)
    {
        var interfaceCounts = new Dictionary<string, int>(System.StringComparer.Ordinal);
        var implCounts = new Dictionary<string, int>(System.StringComparer.Ordinal);
        foreach (var target in targets)
        {
            var ifaceName = GetGenericConstraintCombinedInterfaceName(target, entryKind, qualified: false);
            var implName = GetGenericConstraintCombinedImplName(target, entryKind, qualified: false);
            interfaceCounts[ifaceName] = interfaceCounts.TryGetValue(ifaceName, out var i) ? i + 1 : 1;
            implCounts[implName] = implCounts.TryGetValue(implName, out var c) ? c + 1 : 1;
        }

        var interfaceNames = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        var implNames = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        foreach (var kvp in interfaceCounts)
        {
            if (kvp.Value > 1)
                interfaceNames.Add(kvp.Key);
        }

        foreach (var kvp in implCounts)
        {
            if (kvp.Value > 1)
                implNames.Add(kvp.Key);
        }

        // A combined type may also not shadow a name the hierarchy itself emits.
        foreach (var desc in hierarchy.Values)
        {
            interfaceNames.Add(desc.InterfaceName);
            implNames.Add(desc.ImplName);
        }

        return (interfaceNames, implNames);
    }

    private static string GenerateGenericConstraintEventSource(
        GenericConstraintTarget target,
        Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
        Compilation compilation,
        Action<string, Location?, string> reportDiagnostic,
        ObservableEventsEntryKind entryKind,
        (System.Collections.Generic.HashSet<string> InterfaceNames, System.Collections.Generic.HashSet<string> ImplNames) collidingNames)
    {
        var combinedIfaceName = GetGenericConstraintCombinedInterfaceName(target, entryKind, qualified: false);
        var implName = GetGenericConstraintCombinedImplName(target, entryKind, qualified: false);
        if (collidingNames.InterfaceNames.Contains(combinedIfaceName)
            || collidingNames.ImplNames.Contains(implName))
        {
            combinedIfaceName = GetGenericConstraintCombinedInterfaceName(target, entryKind, qualified: true);
            implName = GetGenericConstraintCombinedImplName(target, entryKind, qualified: true);
        }

        var willEmit = BuildWillEmitInterfaceSet(hierarchy, compilation, entryKind);
        var parentBases = new List<string>();
        foreach (var ct in target.ConstraintTypes)
        {
            var def = ct.IsGenericType ? (INamedTypeSymbol)ct.OriginalDefinition : ct;
            if (hierarchy.TryGetValue(def, out var pd)
                && willEmit.Contains(pd)
                && GetParentInterfaceReference(pd, ct, compilation) is { } parentRef)
            {
                parentBases.Add(parentRef);
            }
        }

        var reuseParent = parentBases.Count == 1
            && string.Equals(StripGenericArity(parentBases[0]), combinedIfaceName, System.StringComparison.Ordinal);
        var ifaceRef = reuseParent ? parentBases[0] : combinedIfaceName;

        var unit = SyntaxFactory.CompilationUnit()
#if EVENTS_R3
            .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("R3")))
#endif
            ;

        var members = new List<MemberDeclarationSyntax>();

        if (!reuseParent)
        {
            var combinedIface = SyntaxFactory.InterfaceDeclaration(combinedIfaceName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.InternalKeyword));
            if (parentBases.Count > 0)
            {
                combinedIface = combinedIface.AddBaseListTypes(
                    parentBases.Select(static n => (BaseTypeSyntax)SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(n))).ToArray());
            }

            members.Add(combinedIface);
        }

        var methodName = entryKind == ObservableEventsEntryKind.Events
            ? ObservableEventsConstants.EventsEntryMethodName
            : ObservableEventsConstants.EventHandlersEntryMethodName;
        var extensionMethod = ObservableEventsSyntaxFactory.CreateFromSenderExtensionMethod(
            methodName,
            SyntaxFactory.ParseTypeName(ifaceRef),
            SyntaxFactory.ParseTypeName("TSource"),
            ObservableEventsSyntaxFactory.NamedGenericType(implName, "TSource"),
            SyntaxFactory.TypeParameterList(
                SyntaxFactory.SingletonSeparatedList(SyntaxFactory.TypeParameter("TSource"))),
            SyntaxFactory.SingletonList(CreateGenericConstraintClauseSyntax(target)));
        members.Add(
            SyntaxFactory.ClassDeclaration("ObservableEventsBootstrapExtensions_" + ToIdentifier(target.Key))
                .AddModifiers(
                    SyntaxFactory.Token(SyntaxKind.InternalKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                    // Events and EventKinds emit the same class name in separate files; partial merges them.
                    SyntaxFactory.Token(SyntaxKind.PartialKeyword))
                .AddMembers(extensionMethod));

        members.Add(CreateGenericConstraintImplClass(
            target, ifaceRef, implName, compilation, reportDiagnostic, entryKind));

        var ns = SyntaxFactory.FileScopedNamespaceDeclaration(SyntaxFactory.ParseName(ObservableEventsConstants.GeneratedNamespace))
            .AddMembers(members.ToArray());
        unit = unit.AddMembers(ns);
        return GeneratedSourceHeader.ToSource(unit);
    }

    private static ClassDeclarationSyntax CreateGenericConstraintImplClass(
        GenericConstraintTarget target,
        string combinedIfaceName,
        string implName,
        Compilation compilation,
        Action<string, Location?, string> reportDiagnostic,
        ObservableEventsEntryKind entryKind)
    {
        var classDecl = SyntaxFactory.ClassDeclaration(implName)
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.InternalKeyword),
                SyntaxFactory.Token(SyntaxKind.SealedKeyword))
            .WithTypeParameterList(
                SyntaxFactory.TypeParameterList(
                    SyntaxFactory.SingletonSeparatedList(SyntaxFactory.TypeParameter("TSource"))))
            .AddConstraintClauses(CreateGenericConstraintClauseSyntax(target))
            .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(combinedIfaceName)));

        var senderType = SyntaxFactory.ParseTypeName("TSource");
        var field = SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(senderType)
                    .AddVariables(SyntaxFactory.VariableDeclarator("_sender")))
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));

        var ctor = SyntaxFactory.ConstructorDeclaration(implName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.InternalKeyword))
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("sender")).WithType(senderType))
            .WithBody(SyntaxFactory.Block(ObservableEventsSyntaxFactory.SenderAssignmentStatement()));

        var memberList = new List<MemberDeclarationSyntax> { field, ctor };
        foreach (var evt in GetGenericConstraintEvents(target, compilation, reportDiagnostic))
        {
            var accessor = ObservableEventsSyntaxFactory.CastSenderMemberAccess(
                SyntaxFactory.ParseTypeName(ObservableEventsConstants.QualifiedType(evt.ContainingType)),
                evt.Name);
            if (entryKind == ObservableEventsEntryKind.Events)
            {
                if (TryCreateEventObservableProperty(evt, accessor, reportDiagnostic, entryKind, out var prop, includeXmlDocumentation: false))
                    memberList.Add(prop);
            }
            else if (TryCreateEventHandlerObservableProperty(evt, accessor, compilation, reportDiagnostic, entryKind, out var prop, includeXmlDocumentation: false))
            {
                memberList.Add(prop);
            }
        }

        return classDecl.AddMembers(memberList.ToArray());
    }

    private static string StripGenericArity(string typeName)
    {
        var index = typeName.IndexOf('<');
        return index < 0 ? typeName : typeName.Substring(0, index);
    }
}
