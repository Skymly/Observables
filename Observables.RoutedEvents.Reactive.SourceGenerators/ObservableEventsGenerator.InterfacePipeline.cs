using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Observables.SourceGenerators.Shared.Diagnostics;
using Observables.SourceGenerators.Shared.Extensions;

namespace Observables.RoutedEvents.Reactive.SourceGenerators;

public sealed partial class ObservableEventsGenerator
{
private static void EmitInterfaceBasedSources(
    ImmutableArray<INamedTypeSymbol> callSiteTypes,
    ImmutableArray<GenericConstraintTarget> genericConstraintTargets,
    Compilation compilation,
    SourceProductionContext context,
    ObservableEventsEntryKind entryKind,
    bool useWpf = false)
{
    var allTypes = callSiteTypes.AddRange(
        genericConstraintTargets.SelectMany(static t => t.ConstraintTypes)
            .Select(static t => t.IsGenericType ? (INamedTypeSymbol)t.OriginalDefinition : t));

    var hierarchy = BuildEventInterfaceHierarchy(allTypes, entryKind, compilation, useWpf);
    if (hierarchy.Count == 0 && genericConstraintTargets.Length == 0)
        return;

    var kindTag = GetEntryKindSourceTag(entryKind);

    if (hierarchy.Count > 0)
    {
        var interfacesSource = GenerateEventInterfacesSource(hierarchy, compilation, context, entryKind);
        if (!string.IsNullOrWhiteSpace(interfacesSource))
            context.AddSource($"EventInterfaces.{kindTag}.g.cs", SourceText.From(interfacesSource, Encoding.UTF8));
    }

    foreach (var type in callSiteTypes)
    {
        var source = GenerateEventImplAndExtensionSource(type, hierarchy, compilation, context, entryKind);
        if (!string.IsNullOrWhiteSpace(source))
            context.AddSource($"{type.GetSafeHintName()}.{kindTag}.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    foreach (var target in genericConstraintTargets)
    {
        var source = GenerateGenericConstraintEventSource(target, hierarchy, compilation, context, entryKind);
        if (!string.IsNullOrWhiteSpace(source))
            context.AddSource($"{GetGenericConstraintTargetHintName(target)}.{kindTag}.g.cs", SourceText.From(source, Encoding.UTF8));
    }
}

// ── Hierarchy building ──────────────────────────────────────────

private static Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> BuildEventInterfaceHierarchy(
    ImmutableArray<INamedTypeSymbol> seedTypes,
    ObservableEventsEntryKind entryKind,
    Compilation compilation,
    bool useWpf)
{
    var result = new Dictionary<INamedTypeSymbol, EventInterfaceDescriptor>(SymbolEqualityComparer.Default);
    foreach (var type in seedTypes)
        ExpandForInterfaces(type, result, entryKind, compilation, useWpf);
    ResolveInterfaceNameCollisions(result, entryKind);
    return result;
}

private static string GetEntryKindSourceTag(ObservableEventsEntryKind entryKind) =>
    entryKind switch
    {
        ObservableEventsEntryKind.FromEvents => "FromEvents",
        ObservableEventsEntryKind.FromEventHandlers => "FromEventHandlers",
        ObservableEventsEntryKind.FromRoutedEvents => "FromRoutedEvents",
        ObservableEventsEntryKind.FromRoutedEventHandlers => "FromRoutedEventHandlers",
        _ => throw new System.ArgumentOutOfRangeException(nameof(entryKind)),
    };

private static string GetInterfaceNameSuffix(ObservableEventsEntryKind entryKind) =>
    entryKind switch
    {
        ObservableEventsEntryKind.FromEvents => "Events",
        ObservableEventsEntryKind.FromEventHandlers => "EventHandlers",
        ObservableEventsEntryKind.FromRoutedEvents => "RoutedEvents",
        ObservableEventsEntryKind.FromRoutedEventHandlers => "RoutedEventHandlers",
        _ => throw new System.ArgumentOutOfRangeException(nameof(entryKind)),
    };

private static bool IsEventIncludedForEntryKind(
    IEventSymbol evt,
    ObservableEventsEntryKind entryKind,
    Compilation compilation,
    bool useWpf) =>
    entryKind switch
    {
        ObservableEventsEntryKind.FromEvents or ObservableEventsEntryKind.FromEventHandlers => true,
        ObservableEventsEntryKind.FromRoutedEvents or ObservableEventsEntryKind.FromRoutedEventHandlers =>
            IsRoutedClrEvent(evt, compilation, useWpf)
            || TryGetAvaloniaRoutedClrEventField(evt, compilation, out _, out _),
        _ => false,
    };

/// <returns>
/// Interface source types reachable through <paramref name="type"/>.
/// If the type gets its own interface, returns just itself.
/// If the type is a pass-through (no own events), returns its ancestor interfaces.
/// </returns>
private static ImmutableArray<INamedTypeSymbol> ExpandForInterfaces(
    INamedTypeSymbol type,
    Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> result,
    ObservableEventsEntryKind entryKind,
    Compilation compilation,
    bool useWpf)
{
    if (result.ContainsKey(type))
        return ImmutableArray.Create(type);
    if (type.SpecialType == SpecialType.System_Object)
        return ImmutableArray<INamedTypeSymbol>.Empty;

    var parentTypes = new List<INamedTypeSymbol>();
    foreach (var parent in GetDirectBaseTypes(type))
    {
        var parentDef = parent.IsGenericType ? (INamedTypeSymbol)parent.OriginalDefinition : parent;
        var contribution = ExpandForInterfaces(parentDef, result, entryKind, compilation, useWpf);
        foreach (var c in contribution)
        {
            if (!parentTypes.Contains(c, SymbolEqualityComparer.Default))
                parentTypes.Add(c);
        }
    }

    var declaredEvents = type.GetMembers()
        .OfType<IEventSymbol>()
        .Where(e => e is
        {
            IsStatic: false,
            DeclaredAccessibility: Accessibility.Public,
            IsOverride: false,
        } && e.ExplicitInterfaceImplementations.IsEmpty
            && IsEventIncludedForEntryKind(e, entryKind, compilation, useWpf))
        .ToList();

    var parentEventNames = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
    foreach (var pt in parentTypes)
    {
        if (result.TryGetValue(pt, out var pd))
            CollectAllInterfaceEventNames(pd, result, parentEventNames);
    }

    var exclusiveEvents = declaredEvents
        .Where(e => !parentEventNames.Contains(e.Name))
        .OrderBy(static e => e.Name, System.StringComparer.Ordinal)
        .ToImmutableArray();

    if (exclusiveEvents.Length == 0 && parentTypes.Count == 0)
        return ImmutableArray<INamedTypeSymbol>.Empty;

    var ifaceName = ComputeRawEventInterfaceName(type, entryKind);
    result[type] = new EventInterfaceDescriptor(type, ifaceName, exclusiveEvents, parentTypes.ToImmutableArray());
    return ImmutableArray.Create(type);
}

private static IEnumerable<INamedTypeSymbol> GetDirectBaseTypes(INamedTypeSymbol type)
{
    if (type.TypeKind != TypeKind.Interface
        && type.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
    {
        yield return baseType;
    }

    foreach (var iface in type.Interfaces)
        yield return iface;
}

private static void CollectAllInterfaceEventNames(
    EventInterfaceDescriptor descriptor,
    Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
    System.Collections.Generic.HashSet<string> names)
{
    foreach (var evt in descriptor.ExclusiveEvents)
        names.Add(evt.Name);
    foreach (var parentType in descriptor.ParentTypes)
    {
        if (hierarchy.TryGetValue(parentType, out var pd))
            CollectAllInterfaceEventNames(pd, hierarchy, names);
    }
}

// ── Interface naming ────────────────────────────────────────────

private static string ComputeRawEventInterfaceName(INamedTypeSymbol type, ObservableEventsEntryKind entryKind)
{
    var suffix = GetInterfaceNameSuffix(entryKind);
    var name = type.Name;
    if (type.TypeKind == TypeKind.Interface && name.Length >= 2 && name[0] == 'I' && char.IsUpper(name[1]))
        return $"{name}{suffix}";
    return $"I{name}{suffix}";
}

private static void ResolveInterfaceNameCollisions(
    Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
    ObservableEventsEntryKind entryKind)
{
    var suffix = GetInterfaceNameSuffix(entryKind);
    var byName = new Dictionary<string, List<INamedTypeSymbol>>(System.StringComparer.Ordinal);
    foreach (var kvp in hierarchy)
    {
        if (!byName.TryGetValue(kvp.Value.InterfaceName, out var list))
        {
            list = new List<INamedTypeSymbol>();
            byName[kvp.Value.InterfaceName] = list;
        }

        list.Add(kvp.Key);
    }

    foreach (var group in byName.Where(static g => g.Value.Count > 1))
    {
        foreach (var type in group.Value)
        {
            var desc = hierarchy[type];
            var nsPrefix = type.ContainingNamespace is { IsGlobalNamespace: false } ns
                ? ns.ToDisplayString().Replace('.', '_')
                : string.Empty;
            var prefix = string.IsNullOrEmpty(nsPrefix) ? desc.InterfaceName : $"I{nsPrefix}_{type.Name}";
            desc.InterfaceName = $"{prefix}{suffix}";
        }
    }
}

private static string GetEventImplName(INamedTypeSymbol type, ObservableEventsEntryKind entryKind)
{
    var suffix = entryKind switch
    {
        ObservableEventsEntryKind.FromEvents => "EventsImpl",
        ObservableEventsEntryKind.FromEventHandlers => "EventHandlersImpl",
        ObservableEventsEntryKind.FromRoutedEvents => "RoutedEventsImpl",
        ObservableEventsEntryKind.FromRoutedEventHandlers => "RoutedEventHandlersImpl",
        _ => throw new System.ArgumentOutOfRangeException(nameof(entryKind)),
    };
    return $"{type.Name}{suffix}";
}

// ── Interface property type ─────────────────────────────────────

private static TypeSyntax? GetEventInterfacePropertyType(
    IEventSymbol evt,
    ObservableEventsEntryKind entryKind,
    Compilation compilation)
{
    if (evt.Type is not INamedTypeSymbol delegateType
        || delegateType.DelegateInvokeMethod is not IMethodSymbol invoke
        || !invoke.ReturnsVoid)
        return null;

    if (entryKind is ObservableEventsEntryKind.FromEvents or ObservableEventsEntryKind.FromRoutedEvents)
        return ObservableEventsSyntaxFactory.GetObservableReturnTypeSyntax(invoke.Parameters);

    if (IsClassicSystemEventHandler(delegateType, compilation, out var genericEventArgs))
    {
        var eventArgsType = genericEventArgs is null
            ? SyntaxFactory.ParseTypeName("global::System.EventArgs")
            : SyntaxFactory.ParseTypeName(ObservableEventsConstants.QualifiedType(genericEventArgs));
        return ObservableEventsSyntaxFactory.ObservableSenderArgsTupleType(eventArgsType);
    }

    if (IsLegacySenderReceiverDelegate(delegateType, invoke, compilation))
        return ObservableEventsSyntaxFactory.GetFromEventHandlersSenderReceiverReturnTypeSyntax(invoke.Parameters);

    return null;
}
}
