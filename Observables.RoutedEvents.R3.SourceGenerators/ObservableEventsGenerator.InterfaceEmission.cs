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

namespace Observables.RoutedEvents.R3.SourceGenerators;

public sealed partial class ObservableEventsGenerator
{
private static string GenerateEventInterfacesSource(
    Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
    Compilation compilation,
    SourceProductionContext context,
    ObservableEventsEntryKind entryKind)
{
    var unit = SyntaxFactory.CompilationUnit()
        .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("R3")));

    var interfaces = new List<MemberDeclarationSyntax>();
    foreach (var desc in hierarchy.Values.OrderBy(static d => d.InterfaceName, System.StringComparer.Ordinal))
    {
        var iface = CreateEventInterface(desc, hierarchy, compilation, entryKind);
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

private static InterfaceDeclarationSyntax? CreateEventInterface(
    EventInterfaceDescriptor descriptor,
    Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
    Compilation compilation,
    ObservableEventsEntryKind entryKind)
{
    var type = descriptor.SourceType;
    var iface = SyntaxFactory.InterfaceDeclaration(descriptor.InterfaceName)
        .AddModifiers(SyntaxFactory.Token(SyntaxKind.InternalKeyword));

    if (type.IsGenericType)
    {
        iface = iface.WithTypeParameterList(
            SyntaxFactory.TypeParameterList(
                SyntaxFactory.SeparatedList(
                    type.TypeParameters.Select(static tp => SyntaxFactory.TypeParameter(tp.Name)))));
    }

    var bases = descriptor.ParentTypes
        .Select(pt =>
        {
            var ptDef = pt.IsGenericType ? (INamedTypeSymbol)pt.OriginalDefinition : pt;
            if (!hierarchy.TryGetValue(ptDef, out var pd)) return null;
            return GetParentInterfaceReference(pd, pt);
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
            SyntaxFactory.PropertyDeclaration(returnType, evt.Name)
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

private static string GetParentInterfaceReference(EventInterfaceDescriptor parentDesc, INamedTypeSymbol constructedParentType)
{
    var name = parentDesc.InterfaceName;
    if (constructedParentType.IsGenericType && constructedParentType.TypeArguments.Length > 0)
        name += $"<{string.Join(", ", constructedParentType.TypeArguments.Select(static ta => ObservableEventsConstants.QualifiedType(ta)))}>";
    return name;
}

// ── Impl class + extension method source file ───────────────────

private static string GenerateEventImplAndExtensionSource(
    INamedTypeSymbol type,
    Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
    Compilation compilation,
    SourceProductionContext context,
    ObservableEventsEntryKind entryKind)
{
    if (!hierarchy.TryGetValue(type, out var desc))
        return string.Empty;

    var implName = GetEventImplName(type, entryKind);
    var typeParamList = type.IsGenericType
        ? $"<{string.Join(", ", type.TypeParameters.Select(static tp => tp.Name))}>"
        : string.Empty;
    var interfaceRef = $"{desc.InterfaceName}{typeParamList}";
    var implRef = $"{implName}{typeParamList}";
    var qualifiedSender = ObservableEventsConstants.QualifiedType(type);

    var unit = SyntaxFactory.CompilationUnit()
        .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("R3")));

    var methodName = entryKind switch
    {
        ObservableEventsEntryKind.Events => ObservableEventsConstants.EventsEntryMethodName,
        ObservableEventsEntryKind.EventHandlers => ObservableEventsConstants.EventHandlersEntryMethodName,
        ObservableEventsEntryKind.RoutedEvents => ObservableEventsConstants.RoutedEventsEntryMethodName,
        ObservableEventsEntryKind.RoutedEventHandlers => ObservableEventsConstants.RoutedEventHandlersEntryMethodName,
        _ => throw new System.ArgumentOutOfRangeException(nameof(entryKind)),
    };

    TypeParameterListSyntax? extensionTypeParams = null;
    if (type.IsGenericType)
    {
        extensionTypeParams = SyntaxFactory.TypeParameterList(
            SyntaxFactory.SeparatedList(
                type.TypeParameters.Select(static tp => SyntaxFactory.TypeParameter(tp.Name))));
    }

    var useAvaloniaRoutedExtension = HasAvaloniaRoutedClrEvents(type, compilation)
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
                objectCreationArguments: ObservableEventsSyntaxFactory.AvaloniaRoutedImplConstructorArguments()));
        extensionMembers.Add(
            ObservableEventsSyntaxFactory.CreateAvaloniaRoutedExtensionMethod(
                methodName,
                SyntaxFactory.ParseTypeName(interfaceRef),
                SyntaxFactory.ParseTypeName(qualifiedSender),
                SyntaxFactory.ParseTypeName(implRef)));
    }
    else
    {
        extensionMembers.Add(
            ObservableEventsSyntaxFactory.CreateFromSenderExtensionMethod(
                methodName,
                SyntaxFactory.ParseTypeName(interfaceRef),
                SyntaxFactory.ParseTypeName(qualifiedSender),
                SyntaxFactory.ParseTypeName(implRef),
                extensionTypeParams));
    }

    var extensionClass = ObservableEventsSyntaxFactory.BootstrapExtensionsClassDeclaration()
        .AddMembers(extensionMembers.ToArray());

    var implClass = CreateEventImplClass(type, desc, implName, hierarchy, compilation, context, entryKind);

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
    SourceProductionContext context,
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
    }

    var senderType = SyntaxFactory.ParseTypeName(ObservableEventsConstants.QualifiedType(type));
    var useAvaloniaRoutedImpl = HasAvaloniaRoutedClrEvents(type, compilation)
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

    foreach (var (evt, accessor) in CollectAllEventsWithAccessor(type, hierarchy))
    {
        switch (entryKind)
        {
            case ObservableEventsEntryKind.Events:
                if (TryCreateEventObservableProperty(evt, accessor, context, out var eventsProp, includeXmlDocumentation: false))
                    members.Add(eventsProp);
                break;
            case ObservableEventsEntryKind.EventHandlers:
                if (TryCreateEventHandlerObservableProperty(evt, accessor, compilation, context, out var eventHandlersProp, includeXmlDocumentation: false))
                    members.Add(eventHandlersProp);
                break;
            case ObservableEventsEntryKind.RoutedEvents:
                if (TryGetAvaloniaRoutedClrEventField(evt, compilation, out var routedEventField, out var eventArgsType))
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
                else if (TryCreateEventObservableProperty(evt, accessor, context, out var routedEventsProp, includeXmlDocumentation: false))
                {
                    members.Add(routedEventsProp);
                }

                break;
            case ObservableEventsEntryKind.RoutedEventHandlers:
                if (TryGetAvaloniaRoutedClrEventField(evt, compilation, out var routedHandlerField, out var handlerArgsType))
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
                else if (TryCreateEventHandlerObservableProperty(evt, accessor, compilation, context, out var routedHandlersProp, includeXmlDocumentation: false))
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
    Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy)
{
    var accessible = new System.Collections.Generic.HashSet<string>(
        GetPublicInstanceEventsFromTypeAndBases(callSiteType).Select(static e => e.Name),
        System.StringComparer.Ordinal);
    var result = new Dictionary<string, (IEventSymbol, ExpressionSyntax)>(System.StringComparer.Ordinal);
    if (hierarchy.TryGetValue(callSiteType, out var desc))
        CollectEventsRecursive(desc, hierarchy, accessible, result);
    return result.Values.OrderBy(static x => x.Item1.Name, System.StringComparer.Ordinal);
}

private static void CollectEventsRecursive(
    EventInterfaceDescriptor desc,
    Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
    System.Collections.Generic.HashSet<string> accessible,
    Dictionary<string, (IEventSymbol, ExpressionSyntax)> result)
{
    foreach (var evt in desc.ExclusiveEvents)
    {
        if (result.ContainsKey(evt.Name)) continue;
        var accessor = accessible.Contains(evt.Name)
            ? ObservableEventsSyntaxFactory.SenderMemberAccess(evt.Name)
            : ObservableEventsSyntaxFactory.CastSenderMemberAccess(
                SyntaxFactory.ParseTypeName(ObservableEventsConstants.QualifiedType(evt.ContainingType)),
                evt.Name);
        result[evt.Name] = (evt, accessor);
    }

    foreach (var parentType in desc.ParentTypes)
    {
        if (hierarchy.TryGetValue(parentType, out var pd))
            CollectEventsRecursive(pd, hierarchy, accessible, result);
    }
}
}
