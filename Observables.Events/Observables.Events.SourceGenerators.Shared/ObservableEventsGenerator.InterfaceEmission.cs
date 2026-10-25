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
    private static string GenerateEventInterfacesSource(
        Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
        Compilation compilation,
        ObservableEventsEntryKind entryKind)
    {
        var unit = SyntaxFactory.CompilationUnit()
#if EVENTS_R3
            .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("R3")))
#endif
            ;

        var willEmit = BuildWillEmitInterfaceSet(hierarchy, compilation, entryKind);
        var interfaces = new List<MemberDeclarationSyntax>();
        foreach (var desc in hierarchy.Values.OrderBy(static d => d.InterfaceName, System.StringComparer.Ordinal))
        {
            var iface = CreateEventInterface(desc, hierarchy, compilation, entryKind, willEmit);
            if (iface is not null)
                interfaces.Add(iface);
        }

        if (interfaces.Count == 0)
            return string.Empty;

        var ns = SyntaxFactory.FileScopedNamespaceDeclaration(SyntaxFactory.ParseName(ObservableEventsConstants.GeneratedNamespace))
            .AddMembers(interfaces.ToArray());
        unit = unit.AddMembers(ns);
        return GeneratedSourceHeader.ToSource(unit);
    }

    /// <summary>
    /// Descriptors that will actually produce an interface: they have at least one emittable
    /// property, or inherit at least one interface that is itself emitted. Evaluated in the
    /// dictionary's registration order, which is post-order (parents are expanded before the
    /// types that reference them).
    /// </summary>
    private static System.Collections.Generic.HashSet<EventInterfaceDescriptor> BuildWillEmitInterfaceSet(
        Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
        Compilation compilation,
        ObservableEventsEntryKind entryKind)
    {
        var set = new System.Collections.Generic.HashSet<EventInterfaceDescriptor>();
        foreach (var desc in hierarchy.Values)
        {
            var hasProps = desc.ExclusiveEvents.Any(e => GetEventInterfacePropertyType(e, entryKind, compilation) is not null);
            var hasEmittedBase = desc.ParentTypes.Any(pt =>
                hierarchy.TryGetValue(HierarchyKey(pt), out var pd) && set.Contains(pd));
            if (hasProps || hasEmittedBase)
                set.Add(desc);
        }

        return set;
    }

    private static InterfaceDeclarationSyntax? CreateEventInterface(
        EventInterfaceDescriptor descriptor,
        Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
        Compilation compilation,
        ObservableEventsEntryKind entryKind,
        System.Collections.Generic.HashSet<EventInterfaceDescriptor> willEmit)
    {
        if (!willEmit.Contains(descriptor))
            return null;
        var type = descriptor.SourceType;
        var iface = SyntaxFactory.InterfaceDeclaration(descriptor.InterfaceName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.InternalKeyword));

        if (type.IsGenericType)
        {
            iface = iface.WithTypeParameterList(
                SyntaxFactory.TypeParameterList(
                    SyntaxFactory.SeparatedList(
                        type.TypeParameters.Select(static tp => SyntaxFactory.TypeParameter(tp.Name)))));
            var clauses = CreateSourceTypeConstraintClauses(type);
            if (clauses.Count > 0)
                iface = iface.WithConstraintClauses(clauses);
        }

        var bases = descriptor.ParentTypes
            .Select(pt =>
            {
                var ptDef = pt.IsGenericType ? (INamedTypeSymbol)pt.OriginalDefinition : pt;
                if (!hierarchy.TryGetValue(ptDef, out var pd) || !willEmit.Contains(pd)) return null;
                return GetParentInterfaceReference(pd, pt, compilation);
            })
            .Where(static n => n is not null)
            .OrderBy(static n => n, System.StringComparer.Ordinal)
            .Select(static n => (BaseTypeSyntax)SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(n!)))
            .ToArray();

        if (bases.Length > 0)
            iface = iface.AddBaseListTypes(bases);

        var props = new List<MemberDeclarationSyntax>();
        foreach (var evt in descriptor.ExclusiveEvents)
        {
            var returnType = GetEventInterfacePropertyType(evt, entryKind, compilation);
            if (returnType is null) continue;
            props.Add(
                SyntaxFactory.PropertyDeclaration(returnType, SyntaxFactory.Identifier(IdentifierHelper.Escape(evt.Name)))
                    .WithLeadingTrivia(ObservableEventsSyntaxFactory.CreateEventInheritDocTrivia(
                        $"{ObservableEventsConstants.QualifiedType(evt.ContainingType)}.{evt.Name}"))
                    .AddAccessorListAccessors(
                        SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))));
        }

        if (props.Count == 0 && bases.Length == 0)
            return null;

        return iface.AddMembers(props.ToArray());
    }

    private static string? GetParentInterfaceReference(
        EventInterfaceDescriptor parentDesc,
        INamedTypeSymbol constructedParentType,
        Compilation compilation)
    {
        var name = parentDesc.InterfaceName;
        if (constructedParentType.IsGenericType && constructedParentType.TypeArguments.Length > 0)
        {
            if (constructedParentType.TypeArguments.Any(ta => !IsTypeNameable(ta, compilation)))
                return null;
            name += $"<{string.Join(", ", constructedParentType.TypeArguments.Select(static ta => ObservableEventsConstants.QualifiedType(ta)))}>";
        }

        return name;
    }

    // ── Impl class + extension method source file ───────────────────

    private static string GenerateEventImplAndExtensionSource(
        INamedTypeSymbol type,
        Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
        Compilation compilation,
        Action<string, Location?, string> reportDiagnostic,
        ObservableEventsEntryKind entryKind)
    {
        // Generated extension methods and impl classes must be able to name the receiver type.
        if (!compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
            return string.Empty;
        if (!hierarchy.TryGetValue(type, out var desc))
            return string.Empty;

        var willEmit = BuildWillEmitInterfaceSet(hierarchy, compilation, entryKind);
        var implName = desc.ImplName;
        var implClass = CreateEventImplClass(type, desc, implName, hierarchy, compilation, reportDiagnostic, entryKind);
        if (!willEmit.Contains(desc))
        {
            foreach (var evt in desc.ExclusiveEvents)
            {
                if (entryKind is ObservableEventsEntryKind.Events or ObservableEventsEntryKind.RoutedEvents)
                    ReportInvalidDelegate(evt, reportDiagnostic, entryKind);
                else
                    ReportInvalidEventHandlersDelegate(evt, reportDiagnostic, entryKind);
            }

            return string.Empty;
        }
        var typeParamList = type.IsGenericType
            ? $"<{string.Join(", ", type.TypeParameters.Select(static tp => tp.Name))}>"
            : string.Empty;
        var interfaceRef = $"{desc.InterfaceName}{typeParamList}";
        var implRef = $"{implName}{typeParamList}";
        var qualifiedSender = ObservableEventsConstants.QualifiedType(type);

        var unit = SyntaxFactory.CompilationUnit()
#if EVENTS_R3
            .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("R3")))
#endif
            ;

        var methodName = entryKind switch
        {
            ObservableEventsEntryKind.Events => ObservableEventsConstants.EventsEntryMethodName,
            ObservableEventsEntryKind.EventHandlers => ObservableEventsConstants.EventHandlersEntryMethodName,
            ObservableEventsEntryKind.RoutedEvents => ObservableEventsConstants.RoutedEventsEntryMethodName,
            ObservableEventsEntryKind.RoutedEventHandlers => ObservableEventsConstants.RoutedEventHandlersEntryMethodName,
            _ => throw new System.ArgumentOutOfRangeException(nameof(entryKind)),
        };

        TypeParameterListSyntax? extensionTypeParams = null;
        var extensionConstraints = CreateSourceTypeConstraintClauses(type);
        if (type.IsGenericType)
        {
            extensionTypeParams = SyntaxFactory.TypeParameterList(
                SyntaxFactory.SeparatedList(
                    type.TypeParameters.Select(static tp => SyntaxFactory.TypeParameter(tp.Name))));
        }

        var useAvaloniaRoutedExtension = HasAvaloniaRoutedClrEvents(type, compilation)
            && entryKind is ObservableEventsEntryKind.RoutedEvents or ObservableEventsEntryKind.RoutedEventHandlers;
        var useWpfRoutedExtension = !useAvaloniaRoutedExtension
            && HasWpfRoutedClrEvents(type, compilation)
            && entryKind is ObservableEventsEntryKind.RoutedEvents or ObservableEventsEntryKind.RoutedEventHandlers;

        var extensionMembers = new List<MemberDeclarationSyntax>();
        if (useAvaloniaRoutedExtension)
        {
            extensionMembers.Add(
                ObservableEventsSyntaxFactory.CreateFromSenderExtensionMethod(
                    methodName,
                    SyntaxFactory.ParseTypeName(interfaceRef),
                    SyntaxFactory.ParseTypeName(qualifiedSender),
                    SyntaxFactory.ParseTypeName(implRef),
                    extensionTypeParams,
                    constraintClauses: extensionConstraints,
                    objectCreationArguments: ObservableEventsSyntaxFactory.AvaloniaRoutedImplConstructorArguments()));
            extensionMembers.Add(
                ObservableEventsSyntaxFactory.CreateAvaloniaRoutedExtensionMethod(
                    methodName,
                    SyntaxFactory.ParseTypeName(interfaceRef),
                    SyntaxFactory.ParseTypeName(qualifiedSender),
                    SyntaxFactory.ParseTypeName(implRef),
                    extensionTypeParams,
                    extensionConstraints));
        }
        else if (useWpfRoutedExtension)
        {
            extensionMembers.Add(
                ObservableEventsSyntaxFactory.CreateWpfRoutedExtensionMethod(
                    methodName,
                    SyntaxFactory.ParseTypeName(interfaceRef),
                    SyntaxFactory.ParseTypeName(qualifiedSender),
                    SyntaxFactory.ParseTypeName(implRef),
                    extensionTypeParams,
                    extensionConstraints));
        }
        else
        {
            extensionMembers.Add(
                ObservableEventsSyntaxFactory.CreateFromSenderExtensionMethod(
                    methodName,
                    SyntaxFactory.ParseTypeName(interfaceRef),
                    SyntaxFactory.ParseTypeName(qualifiedSender),
                    SyntaxFactory.ParseTypeName(implRef),
                    extensionTypeParams,
                    constraintClauses: extensionConstraints));
        }

        var extensionClass = ObservableEventsSyntaxFactory.BootstrapExtensionsClassDeclaration()
            .AddMembers(extensionMembers.ToArray());

        var ns = SyntaxFactory.FileScopedNamespaceDeclaration(SyntaxFactory.ParseName(ObservableEventsConstants.GeneratedNamespace))
            .AddMembers(extensionClass, implClass);
        unit = unit.AddMembers(ns);
        return GeneratedSourceHeader.ToSource(unit);
    }

    private static ClassDeclarationSyntax CreateEventImplClass(
        INamedTypeSymbol type,
        EventInterfaceDescriptor descriptor,
        string implName,
        Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
        Compilation compilation,
        Action<string, Location?, string> reportDiagnostic,
        ObservableEventsEntryKind entryKind)
    {
        var typeParamList = type.IsGenericType
            ? $"<{string.Join(", ", type.TypeParameters.Select(static tp => tp.Name))}>"
            : string.Empty;
        var interfaceRef = $"{descriptor.InterfaceName}{typeParamList}";

        var classDecl = SyntaxFactory.ClassDeclaration(implName)
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.InternalKeyword),
                SyntaxFactory.Token(SyntaxKind.SealedKeyword))
            .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(interfaceRef)));

        if (type.IsGenericType)
        {
            classDecl = classDecl.WithTypeParameterList(
                SyntaxFactory.TypeParameterList(
                    SyntaxFactory.SeparatedList(
                        type.TypeParameters.Select(static tp => SyntaxFactory.TypeParameter(tp.Name)))));
            var clauses = CreateSourceTypeConstraintClauses(type);
            if (clauses.Count > 0)
                classDecl = classDecl.WithConstraintClauses(clauses);
        }

        var senderType = SyntaxFactory.ParseTypeName(ObservableEventsConstants.QualifiedType(type));
        var useAvaloniaRoutedImpl = HasAvaloniaRoutedClrEvents(type, compilation)
            && entryKind is ObservableEventsEntryKind.RoutedEvents or ObservableEventsEntryKind.RoutedEventHandlers;
        var useWpfRoutedImpl = !useAvaloniaRoutedImpl
            && HasWpfRoutedClrEvents(type, compilation)
            && entryKind is ObservableEventsEntryKind.RoutedEvents or ObservableEventsEntryKind.RoutedEventHandlers;

        var members = new List<MemberDeclarationSyntax>();
        if (useAvaloniaRoutedImpl)
        {
            var routesType = SyntaxFactory.ParseTypeName("global::Avalonia.Interactivity.RoutingStrategies");
            members.Add(
                SyntaxFactory.FieldDeclaration(
                        SyntaxFactory.VariableDeclaration(senderType)
                            .AddVariables(SyntaxFactory.VariableDeclarator("_sender")))
                    .AddModifiers(
                        SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                        SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));
            members.Add(
                SyntaxFactory.FieldDeclaration(
                        SyntaxFactory.VariableDeclaration(routesType)
                            .AddVariables(SyntaxFactory.VariableDeclarator("_routes")))
                    .AddModifiers(
                        SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                        SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));
            members.Add(
                SyntaxFactory.FieldDeclaration(
                        SyntaxFactory.VariableDeclaration(
                                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)))
                            .AddVariables(SyntaxFactory.VariableDeclarator("_handledEventsToo")))
                    .AddModifiers(
                        SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                        SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));

            members.Add(
                SyntaxFactory.ConstructorDeclaration(implName)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.InternalKeyword))
                    .AddParameterListParameters(
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("sender")).WithType(senderType),
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("routes")).WithType(routesType),
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("handledEventsToo"))
                            .WithType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword))))
                    .WithBody(
                        SyntaxFactory.Block(
                            ObservableEventsSyntaxFactory.SenderAssignmentStatement(),
                            SyntaxFactory.ExpressionStatement(
                                SyntaxFactory.AssignmentExpression(
                                    SyntaxKind.SimpleAssignmentExpression,
                                    SyntaxFactory.IdentifierName("_routes"),
                                    SyntaxFactory.IdentifierName("routes"))),
                            SyntaxFactory.ExpressionStatement(
                                SyntaxFactory.AssignmentExpression(
                                    SyntaxKind.SimpleAssignmentExpression,
                                    SyntaxFactory.IdentifierName("_handledEventsToo"),
                                    SyntaxFactory.IdentifierName("handledEventsToo"))))));
        }
        else if (useWpfRoutedImpl)
        {
            members.Add(
                SyntaxFactory.FieldDeclaration(
                        SyntaxFactory.VariableDeclaration(senderType)
                            .AddVariables(SyntaxFactory.VariableDeclarator("_sender")))
                    .AddModifiers(
                        SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                        SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));
            members.Add(
                SyntaxFactory.FieldDeclaration(
                        SyntaxFactory.VariableDeclaration(
                                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)))
                            .AddVariables(SyntaxFactory.VariableDeclarator("_handledEventsToo")))
                    .AddModifiers(
                        SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                        SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));

            members.Add(
                SyntaxFactory.ConstructorDeclaration(implName)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.InternalKeyword))
                    .AddParameterListParameters(
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("sender")).WithType(senderType),
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("handledEventsToo"))
                            .WithType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword))))
                    .WithBody(
                        SyntaxFactory.Block(
                            ObservableEventsSyntaxFactory.SenderAssignmentStatement(),
                            SyntaxFactory.ExpressionStatement(
                                SyntaxFactory.AssignmentExpression(
                                    SyntaxKind.SimpleAssignmentExpression,
                                    SyntaxFactory.IdentifierName("_handledEventsToo"),
                                    SyntaxFactory.IdentifierName("handledEventsToo"))))));
        }
        else
        {
            members.Add(
                SyntaxFactory.FieldDeclaration(
                        SyntaxFactory.VariableDeclaration(senderType)
                            .AddVariables(SyntaxFactory.VariableDeclarator("_sender")))
                    .AddModifiers(
                        SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                        SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));

            members.Add(
                SyntaxFactory.ConstructorDeclaration(implName)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.InternalKeyword))
                    .AddParameterListParameters(
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("sender")).WithType(senderType))
                    .WithBody(SyntaxFactory.Block(ObservableEventsSyntaxFactory.SenderAssignmentStatement())));
        }

        foreach (var (evt, accessor) in CollectAllEventsWithAccessor(type, hierarchy, compilation))
        {
            switch (entryKind)
            {
                case ObservableEventsEntryKind.Events:
                    if (TryCreateEventObservableProperty(evt, accessor, reportDiagnostic, entryKind, out var eventsProp, includeXmlDocumentation: false))
                        members.Add(eventsProp);
                    break;
                case ObservableEventsEntryKind.EventHandlers:
                    if (TryCreateEventHandlerObservableProperty(evt, accessor, compilation, reportDiagnostic, entryKind, out var eventHandlersProp, includeXmlDocumentation: false))
                        members.Add(eventHandlersProp);
                    break;
                case ObservableEventsEntryKind.RoutedEvents:
                    if (TryGetAvaloniaRoutedClrEventField(evt, compilation, out var routedEventField, out var eventArgsType))
                    {
                        if (GetEventInterfacePropertyType(evt, entryKind, compilation) is null)
                        {
                            ReportInvalidDelegate(evt, reportDiagnostic, entryKind);
                        }
                        else
                        {
                            members.Add(
                                ObservableEventsSyntaxFactory.CreateAvaloniaRoutedEventProperty(
                                    evt,
                                    routedEventField,
                                    eventArgsType,
                                    useEventHandlers: false,
                                    ObservableEventsSyntaxFactory.CreateEventInheritDocTrivia(
                                        $"{ObservableEventsConstants.QualifiedType(evt.ContainingType)}.{evt.Name}")));
                        }
                    }
                    else if (TryGetWpfRoutedClrEventField(evt, compilation, out var wpfRoutedEventField, out var wpfEventArgsType))
                    {
                        members.Add(
                            ObservableEventsSyntaxFactory.CreateWpfRoutedEventProperty(
                                evt,
                                wpfRoutedEventField,
                                wpfEventArgsType,
                                useEventHandlers: false,
                                ObservableEventsSyntaxFactory.CreateEventInheritDocTrivia(
                                    $"{ObservableEventsConstants.QualifiedType(evt.ContainingType)}.{evt.Name}")));
                    }
                    else if (TryCreateEventObservableProperty(evt, accessor, reportDiagnostic, entryKind, out var routedEventsProp, includeXmlDocumentation: false))
                    {
                        members.Add(routedEventsProp);
                    }

                    break;
                case ObservableEventsEntryKind.RoutedEventHandlers:
                    if (TryGetAvaloniaRoutedClrEventField(evt, compilation, out var routedHandlerField, out var handlerArgsType))
                    {
                        if (GetEventInterfacePropertyType(evt, entryKind, compilation) is null)
                        {
                            ReportInvalidEventHandlersDelegate(evt, reportDiagnostic, entryKind);
                        }
                        else
                        {
                            members.Add(
                                ObservableEventsSyntaxFactory.CreateAvaloniaRoutedEventProperty(
                                    evt,
                                    routedHandlerField,
                                    handlerArgsType,
                                    useEventHandlers: true,
                                    ObservableEventsSyntaxFactory.CreateEventInheritDocTrivia(
                                        $"{ObservableEventsConstants.QualifiedType(evt.ContainingType)}.{evt.Name}")));
                        }
                    }
                    else if (TryGetWpfRoutedClrEventField(evt, compilation, out var wpfRoutedHandlerField, out var wpfHandlerArgsType))
                    {
                        members.Add(
                            ObservableEventsSyntaxFactory.CreateWpfRoutedEventProperty(
                                evt,
                                wpfRoutedHandlerField,
                                wpfHandlerArgsType,
                                useEventHandlers: true,
                                ObservableEventsSyntaxFactory.CreateEventInheritDocTrivia(
                                    $"{ObservableEventsConstants.QualifiedType(evt.ContainingType)}.{evt.Name}")));
                    }
                    else if (TryCreateEventHandlerObservableProperty(evt, accessor, compilation, reportDiagnostic, entryKind, out var routedHandlersProp, includeXmlDocumentation: false))
                    {
                        members.Add(routedHandlersProp);
                    }

                    break;
            }
        }

        return classDecl.AddMembers(members.ToArray());
    }

    private static IEnumerable<(IEventSymbol Event, ExpressionSyntax Accessor)> CollectAllEventsWithAccessor(
        INamedTypeSymbol callSiteType,
        Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
        Compilation compilation)
    {
        // Collect the most-derived member per event name regardless of accessibility: a
        // non-public declaration shadows the accessible base event it hides, and _sender.Name
        // would bind the shadowing member.
        var runtimeEvents = new Dictionary<string, IEventSymbol>(System.StringComparer.Ordinal);
        for (var current = callSiteType; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var evt in current.GetMembers().OfType<IEventSymbol>())
            {
                if (evt.IsStatic || runtimeEvents.ContainsKey(evt.Name))
                    continue;
                runtimeEvents[evt.Name] = evt;
            }
        }

        var result = new Dictionary<string, (IEventSymbol, ExpressionSyntax)>(System.StringComparer.Ordinal);
        if (hierarchy.TryGetValue(callSiteType, out var desc))
            CollectEventsRecursive(desc, hierarchy, runtimeEvents, result, callSiteType, callSiteType, compilation);
        return result.Values.OrderBy(static x => x.Item1.Name, System.StringComparer.Ordinal);
    }

    /// <summary>
    /// Resolves the accessor for each merged event. Prefer direct <c>_sender.Name</c> when the
    /// runtime member is reachable through the receiver type; otherwise cast the sender to an
    /// accessible declaring type (interface/explicit events, or a hidden member's base). Events
    /// with no reachable member or no nameable signature are skipped — the interface side never
    /// declares them either because <c>ExclusiveEvents</c> only carries accessible members.
    /// </summary>
    private static void CollectEventsRecursive(
        EventInterfaceDescriptor desc,
        Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
        Dictionary<string, IEventSymbol> runtimeEvents,
        Dictionary<string, (IEventSymbol, ExpressionSyntax)> result,
        INamedTypeSymbol constructedType,
        INamedTypeSymbol callSiteType,
        Compilation compilation)
    {
        foreach (var evt in desc.ExclusiveEvents)
        {
            if (result.ContainsKey(evt.Name)) continue;
            var mapped = MapEventToConstructedType(evt, constructedType);
            if (!IsEventSignatureNameable(mapped, compilation)) continue;

            ExpressionSyntax? accessor = null;
            if (runtimeEvents.TryGetValue(evt.Name, out var runtime)
                && SymbolEqualityComparer.Default.Equals(runtime.Type, mapped.Type)
                && compilation.IsSymbolAccessibleWithin(runtime, compilation.Assembly, callSiteType))
            {
                accessor = ObservableEventsSyntaxFactory.SenderMemberAccess(evt.Name);
            }
            else
            {
                var castEvent = compilation.IsSymbolAccessibleWithin(mapped, compilation.Assembly) ? mapped
                    : compilation.IsSymbolAccessibleWithin(evt, compilation.Assembly) ? evt
                    : null;
                if (castEvent is not null)
                {
                    accessor = ObservableEventsSyntaxFactory.CastSenderMemberAccess(
                        SyntaxFactory.ParseTypeName(ObservableEventsConstants.QualifiedType(castEvent.ContainingType)),
                        evt.Name);
                }
            }

            if (accessor is null) continue;
            result[evt.Name] = (mapped, accessor);
        }

        foreach (var parentType in desc.ParentTypes)
        {
            if (hierarchy.TryGetValue(HierarchyKey(parentType), out var pd))
                CollectEventsRecursive(pd, hierarchy, runtimeEvents, result, parentType, callSiteType, compilation);
        }
    }

    private static IEventSymbol MapEventToConstructedType(IEventSymbol evt, INamedTypeSymbol constructedType)
    {
        foreach (var member in constructedType.GetMembers(evt.Name).OfType<IEventSymbol>())
            return member;
        return evt;
    }
}
