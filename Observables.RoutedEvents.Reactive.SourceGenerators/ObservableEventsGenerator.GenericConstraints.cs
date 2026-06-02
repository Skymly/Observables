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

namespace Observables.RoutedEvents.Reactive.SourceGenerators;

public sealed partial class ObservableEventsGenerator
{
private static IEnumerable<IEventSymbol> GetGenericConstraintEvents(GenericConstraintTarget target)
{
    var byName = new Dictionary<string, IEventSymbol>(System.StringComparer.Ordinal);
    foreach (var constraintType in target.ConstraintTypes)
    {
        foreach (var evt in GetPublicInstanceEventsFromTypeAndBases(constraintType))
        {
            if (!byName.ContainsKey(evt.Name))
            {
                byName[evt.Name] = evt;
            }
        }
    }

    return byName.Values.OrderBy(static e => e.Name, System.StringComparer.Ordinal);
}
private static string GenerateGenericConstraintEventSource(
    GenericConstraintTarget target,
    Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
    Compilation compilation,
    SourceProductionContext context,
    ObservableEventsEntryKind entryKind)
{
    var suffix = entryKind == ObservableEventsEntryKind.Events ? "Events" : "EventHandlers";
    var constraintParts = target.ConstraintTypes.Select(static t =>
    {
        var n = t.Name;
        return t.TypeKind == TypeKind.Interface && n.Length >= 2 && n[0] == 'I' && char.IsUpper(n[1])
            ? n.Substring(1)
            : n;
    });
    var combinedIfaceName = $"I{string.Join("_", constraintParts)}{suffix}";
    var implName = $"{string.Join("_", constraintParts)}{suffix}Impl";

    var parentBases = new List<string>();
    foreach (var ct in target.ConstraintTypes)
    {
        var def = ct.IsGenericType ? (INamedTypeSymbol)ct.OriginalDefinition : ct;
        if (hierarchy.TryGetValue(def, out var pd))
            parentBases.Add(GetParentInterfaceReference(pd, ct));
    }

    var unit = SyntaxFactory.CompilationUnit()
        .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Reactive.Linq")));

    var members = new List<MemberDeclarationSyntax>();

    var combinedIface = SyntaxFactory.InterfaceDeclaration(combinedIfaceName)
        .AddModifiers(SyntaxFactory.Token(SyntaxKind.InternalKeyword));
    if (parentBases.Count > 0)
    {
        combinedIface = combinedIface.AddBaseListTypes(
            parentBases.Select(static n => (BaseTypeSyntax)SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(n))).ToArray());
    }

    members.Add(combinedIface);

    var methodName = entryKind == ObservableEventsEntryKind.Events
        ? ObservableEventsConstants.EventsEntryMethodName
        : ObservableEventsConstants.EventHandlersEntryMethodName;
    var extensionMethod = ObservableEventsSyntaxFactory.CreateFromSenderExtensionMethod(
        methodName,
        SyntaxFactory.ParseTypeName(combinedIfaceName),
        SyntaxFactory.ParseTypeName("TSource"),
        ObservableEventsSyntaxFactory.NamedGenericType(implName, "TSource"),
        SyntaxFactory.TypeParameterList(
            SyntaxFactory.SingletonSeparatedList(SyntaxFactory.TypeParameter("TSource"))),
        SyntaxFactory.SingletonList(CreateGenericConstraintClauseSyntax(target)));
    members.Add(
        ObservableEventsSyntaxFactory.BootstrapExtensionsClassDeclaration()
            .AddMembers(extensionMethod));

    members.Add(CreateGenericConstraintImplClass(
        target, combinedIfaceName, implName, compilation, context, entryKind));

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
    SourceProductionContext context,
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
    foreach (var evt in GetGenericConstraintEvents(target))
    {
        var accessor = ObservableEventsSyntaxFactory.CastSenderMemberAccess(
            SyntaxFactory.ParseTypeName(ObservableEventsConstants.QualifiedType(evt.ContainingType)),
            evt.Name);
        if (entryKind == ObservableEventsEntryKind.Events)
        {
            if (TryCreateEventObservableProperty(evt, accessor, context, out var prop, includeXmlDocumentation: false))
                memberList.Add(prop);
        }
        else if (TryCreateEventHandlerObservableProperty(evt, accessor, compilation, context, out var prop, includeXmlDocumentation: false))
        {
            memberList.Add(prop);
        }
    }

    return classDecl.AddMembers(memberList.ToArray());
}
}
