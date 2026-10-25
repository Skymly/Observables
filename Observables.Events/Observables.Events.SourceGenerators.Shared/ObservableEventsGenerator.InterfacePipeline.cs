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

namespace Observables.Events.Generators;

public sealed partial class ObservableEventsGenerator
{
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
        ResolveImplNameCollisions(result, entryKind);
        return result;
    }

    private static string GetEntryKindSourceTag(ObservableEventsEntryKind entryKind) =>
        entryKind switch
        {
            ObservableEventsEntryKind.Events => "Events",
            ObservableEventsEntryKind.EventHandlers => "EventHandlers",
            ObservableEventsEntryKind.RoutedEvents => "RoutedEvents",
            ObservableEventsEntryKind.RoutedEventHandlers => "RoutedEventHandlers",
            _ => throw new System.ArgumentOutOfRangeException(nameof(entryKind)),
        };

    private static string GetInterfaceNameSuffix(ObservableEventsEntryKind entryKind) =>
        entryKind switch
        {
            ObservableEventsEntryKind.Events => "Events",
            ObservableEventsEntryKind.EventHandlers => "EventHandlers",
            ObservableEventsEntryKind.RoutedEvents => "RoutedEvents",
            ObservableEventsEntryKind.RoutedEventHandlers => "RoutedEventHandlers",
            _ => throw new System.ArgumentOutOfRangeException(nameof(entryKind)),
        };

    private static bool IsEventIncludedForEntryKind(
        IEventSymbol evt,
        ObservableEventsEntryKind entryKind,
        Compilation compilation,
        bool useWpf) =>
        entryKind switch
        {
            ObservableEventsEntryKind.Events or ObservableEventsEntryKind.EventHandlers => true,
            ObservableEventsEntryKind.RoutedEvents or ObservableEventsEntryKind.RoutedEventHandlers =>
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
            var parentDef = HierarchyKey(parent);
            var contribution = ExpandForInterfaces(parentDef, result, entryKind, compilation, useWpf);
            if (contribution.Length > 0 && !parentTypes.Contains(parent, SymbolEqualityComparer.Default))
                parentTypes.Add(parent);
        }

        var declaredEvents = type.GetMembers()
            .OfType<IEventSymbol>()
            .Where(e => e is
            {
                IsStatic: false,
                IsOverride: false,
            } && e.ExplicitInterfaceImplementations.IsEmpty
                && compilation.IsSymbolAccessibleWithin(e, compilation.Assembly)
                && IsEventIncludedForEntryKind(e, entryKind, compilation, useWpf))
            .ToList();

        // Merge every parent's (already built) event surface. A name inherited through more than
        // one parent with an incompatible delegate type cannot be emitted on a single generated
        // interface — drop those parents from the base list, flatten their remaining events into
        // this interface's own set, and record the skipped events for OBS2006 reporting.
        var skippedConflictingEvents = new List<IEventSymbol>();
        var flattenedParentEvents = new Dictionary<string, IEventSymbol>(System.StringComparer.Ordinal);
        if (parentTypes.Count > 1)
        {
            var perParentEvents = new List<(INamedTypeSymbol Parent, Dictionary<string, IEventSymbol> Events)>(parentTypes.Count);
            var merged = new Dictionary<string, IEventSymbol>(System.StringComparer.Ordinal);
            var conflictingNames = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            foreach (var pt in parentTypes)
            {
                var ptEvents = new Dictionary<string, IEventSymbol>(System.StringComparer.Ordinal);
                if (result.TryGetValue(HierarchyKey(pt), out var pd))
                    CollectInterfaceEventsRecursive(pd, result, ptEvents);
                perParentEvents.Add((pt, ptEvents));
                foreach (var kvp in ptEvents)
                {
                    if (merged.TryGetValue(kvp.Key, out var existing))
                    {
                        if (!SymbolEqualityComparer.Default.Equals(existing.Type, kvp.Value.Type))
                            conflictingNames.Add(kvp.Key);
                    }
                    else
                    {
                        merged[kvp.Key] = kvp.Value;
                    }
                }
            }

            if (conflictingNames.Count > 0)
            {
                var keptParents = new List<INamedTypeSymbol>();
                foreach (var (parent, events) in perParentEvents)
                {
                    if (!events.Keys.Any(conflictingNames.Contains))
                    {
                        keptParents.Add(parent);
                        continue;
                    }

                    foreach (var kvp in events)
                    {
                        if (conflictingNames.Contains(kvp.Key))
                            skippedConflictingEvents.Add(kvp.Value);
                        else if (!flattenedParentEvents.ContainsKey(kvp.Key))
                            flattenedParentEvents[kvp.Key] = kvp.Value;
                    }
                }

                parentTypes = keptParents;
            }
        }

        var inheritedEvents = new Dictionary<string, IEventSymbol>(System.StringComparer.Ordinal);
        foreach (var pt in parentTypes)
        {
            if (result.TryGetValue(HierarchyKey(pt), out var pd))
                CollectInterfaceEventsRecursive(pd, result, inheritedEvents);
        }

        var parentEventNames = inheritedEvents.Keys;
        var exclusiveEvents = declaredEvents
            .Where(e => !parentEventNames.Contains(e.Name))
            .Concat(flattenedParentEvents.Values.Where(fe =>
                !parentEventNames.Contains(fe.Name)
                && declaredEvents.All(de => de.Name != fe.Name)))
            .OrderBy(static e => e.Name, System.StringComparer.Ordinal)
            .ToImmutableArray();

        if (exclusiveEvents.Length == 0 && parentTypes.Count == 0)
            return ImmutableArray<INamedTypeSymbol>.Empty;

        var ifaceName = ComputeRawEventInterfaceName(type, entryKind);
        var implName = GetEventImplName(type, entryKind);
        result[type] = new EventInterfaceDescriptor(
            type,
            ifaceName,
            implName,
            exclusiveEvents,
            parentTypes.ToImmutableArray(),
            skippedConflictingEvents.ToImmutableArray());
        return ImmutableArray.Create(type);
    }


    private static void ReportIncompatibleEventMerges(
        Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
        Action<string, Location?, string> reportDiagnostic)
    {
        foreach (var desc in hierarchy.Values)
        {
            foreach (var skipped in desc.SkippedConflictingEvents)
            {
                reportDiagnostic(DiagnosticDescriptors.IncompatibleEventMerge.Id, skipped.Locations.FirstOrDefault(), skipped.Name);
            }

            var byName = new Dictionary<string, IEventSymbol>(System.StringComparer.Ordinal);
            CollectExclusiveEventsRecursive(desc, hierarchy, byName, reportDiagnostic);
            foreach (var declared in desc.SourceType.GetMembers().OfType<IEventSymbol>()
                         .Where(static e => e is { IsStatic: false, DeclaredAccessibility: Accessibility.Public }))
            {
                if (byName.TryGetValue(declared.Name, out var existing)
                    && !SymbolEqualityComparer.Default.Equals(existing.Type, declared.Type))
                {
                    reportDiagnostic(DiagnosticDescriptors.IncompatibleEventMerge.Id, declared.Locations.FirstOrDefault(), declared.Name);
                }
            }
        }
    }

    private static void CollectExclusiveEventsRecursive(
        EventInterfaceDescriptor desc,
        Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
        Dictionary<string, IEventSymbol> byName,
        Action<string, Location?, string> reportDiagnostic)
    {
        foreach (var evt in desc.ExclusiveEvents)
        {
            if (byName.TryGetValue(evt.Name, out var existing)
                && !SymbolEqualityComparer.Default.Equals(existing.Type, evt.Type))
            {
                reportDiagnostic(DiagnosticDescriptors.IncompatibleEventMerge.Id, evt.Locations.FirstOrDefault(), evt.Name);
                continue;
            }

            byName[evt.Name] = evt;
        }

        foreach (var parentType in desc.ParentTypes)
        {
            if (hierarchy.TryGetValue(HierarchyKey(parentType), out var pd))
                CollectExclusiveEventsRecursive(pd, hierarchy, byName, reportDiagnostic);
        }
    }

    private static INamedTypeSymbol HierarchyKey(INamedTypeSymbol type) =>
        type.IsGenericType ? (INamedTypeSymbol)type.OriginalDefinition : type;

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

    /// <summary>
    /// Collects all event symbols contributed by <paramref name="desc"/> and its parents —
    /// first declaration wins on duplicate names.
    /// </summary>
    private static void CollectInterfaceEventsRecursive(
        EventInterfaceDescriptor desc,
        Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
        Dictionary<string, IEventSymbol> events)
    {
        foreach (var evt in desc.ExclusiveEvents)
        {
            if (!events.ContainsKey(evt.Name))
                events[evt.Name] = evt;
        }

        foreach (var parentType in desc.ParentTypes)
        {
            if (hierarchy.TryGetValue(HierarchyKey(parentType), out var pd))
                CollectInterfaceEventsRecursive(pd, hierarchy, events);
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
                var identity = TypeIdentityPrefix(type);
                desc.InterfaceName = string.IsNullOrEmpty(identity)
                    ? $"{desc.InterfaceName}_{type.Name}{suffix}"
                    : $"I{identity}_{type.Name}{suffix}";
            }
        }
    }

    private static void ResolveImplNameCollisions(
        Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
        ObservableEventsEntryKind entryKind)
    {
        var suffix = entryKind switch
        {
            ObservableEventsEntryKind.Events => "EventsImpl",
            ObservableEventsEntryKind.EventHandlers => "EventHandlersImpl",
            ObservableEventsEntryKind.RoutedEvents => "RoutedEventsImpl",
            ObservableEventsEntryKind.RoutedEventHandlers => "RoutedEventHandlersImpl",
            _ => throw new System.ArgumentOutOfRangeException(nameof(entryKind)),
        };

        var byName = new Dictionary<string, List<INamedTypeSymbol>>(System.StringComparer.Ordinal);
        foreach (var kvp in hierarchy)
        {
            if (!byName.TryGetValue(kvp.Value.ImplName, out var list))
            {
                list = new List<INamedTypeSymbol>();
                byName[kvp.Value.ImplName] = list;
            }

            list.Add(kvp.Key);
        }

        foreach (var group in byName.Where(static g => g.Value.Count > 1))
        {
            foreach (var type in group.Value)
            {
                var desc = hierarchy[type];
                var identity = TypeIdentityPrefix(type);
                if (identity.Length == 0)
                    continue;
                desc.ImplName = $"{identity}_{type.Name}{suffix}";
            }
        }
    }

    private static string TypeIdentityPrefix(INamedTypeSymbol type)
    {
        var nested = string.Empty;
        var containing = type.ContainingType;
        while (containing is not null)
        {
            nested = nested.Length == 0 ? containing.Name : containing.Name + "_" + nested;
            containing = containing.ContainingType;
        }

        if (type.ContainingNamespace is { IsGlobalNamespace: false } ns)
        {
            var nsPrefix = ns.ToDisplayString().Replace('.', '_');
            return nested.Length == 0 ? nsPrefix : nsPrefix + "_" + nested;
        }

        return nested;
    }

    private static string GetEventImplName(INamedTypeSymbol type, ObservableEventsEntryKind entryKind)
    {
        var suffix = entryKind switch
        {
            ObservableEventsEntryKind.Events => "EventsImpl",
            ObservableEventsEntryKind.EventHandlers => "EventHandlersImpl",
            ObservableEventsEntryKind.RoutedEvents => "RoutedEventsImpl",
            ObservableEventsEntryKind.RoutedEventHandlers => "RoutedEventHandlersImpl",
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
        if (!IsEventSignatureNameable(evt, compilation)
            || evt.Type is not INamedTypeSymbol delegateType
            || delegateType.DelegateInvokeMethod is not IMethodSymbol invoke
            || !invoke.ReturnsVoid
            || invoke.Parameters.Any(static p => p.RefKind != RefKind.None))
            return null;

        if (entryKind is ObservableEventsEntryKind.Events or ObservableEventsEntryKind.RoutedEvents)
            return ObservableEventsSyntaxFactory.GetObservableReturnTypeSyntax(invoke.Parameters);

        if (IsClassicSystemEventHandler(delegateType, compilation, out var genericEventArgs))
        {
            var eventArgsType = genericEventArgs is null
                ? SyntaxFactory.ParseTypeName("global::System.EventArgs")
                : SyntaxFactory.ParseTypeName(ObservableEventsConstants.QualifiedType(genericEventArgs));
            return ObservableEventsSyntaxFactory.ObservableSenderArgsTupleType(eventArgsType);
        }

        if (IsLegacySenderReceiverDelegate(delegateType, invoke, compilation))
            return ObservableEventsSyntaxFactory.GetEventHandlersSenderReceiverReturnTypeSyntax(invoke.Parameters);

        return null;
    }
}
