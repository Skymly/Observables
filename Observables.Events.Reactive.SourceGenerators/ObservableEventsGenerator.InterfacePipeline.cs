using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Observables.SourceGenerators.Shared;
using Observables.SourceGenerators.Shared.Extensions;

namespace Observables.Events.Reactive.SourceGenerators;

public sealed partial class ObservableEventsGenerator
{
    private static void EmitInterfaceBasedSources(
        ImmutableArray<INamedTypeSymbol> callSiteTypes,
        ImmutableArray<GenericConstraintTarget> genericConstraintTargets,
        Compilation compilation,
        SourceProductionContext context,
        ObservableEventsEntryKind entryKind)
    {
        var allTypes = callSiteTypes.AddRange(
            genericConstraintTargets.SelectMany(static t => t.ConstraintTypes)
                .Select(static t => t.IsGenericType ? (INamedTypeSymbol)t.OriginalDefinition : t));

        var hierarchy = BuildEventInterfaceHierarchy(allTypes, entryKind, compilation);
        if (hierarchy.Count == 0 && genericConstraintTargets.Length == 0)
        {
            return;
        }

        var kindTag = GetEntryKindSourceTag(entryKind);

        if (hierarchy.Count > 0)
        {
            var interfacesSource = GenerateEventInterfacesSource(hierarchy, compilation, context, entryKind);
            if (!string.IsNullOrWhiteSpace(interfacesSource))
            {
                context.AddSource($"EventInterfaces.{kindTag}.g.cs", SourceText.From(interfacesSource, Encoding.UTF8));
            }
        }

        foreach (var type in callSiteTypes)
        {
            var source = GenerateEventImplAndExtensionSource(type, hierarchy, compilation, context, entryKind);
            if (!string.IsNullOrWhiteSpace(source))
            {
                context.AddSource($"{type.GetSafeHintName()}.{kindTag}.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        }

        foreach (var target in genericConstraintTargets)
        {
            var source = GenerateGenericConstraintEventSource(target, hierarchy, compilation, context, entryKind);
            if (!string.IsNullOrWhiteSpace(source))
            {
                context.AddSource($"{GetGenericConstraintTargetHintName(target)}.{kindTag}.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        }
    }

    private static Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> BuildEventInterfaceHierarchy(
        ImmutableArray<INamedTypeSymbol> seedTypes,
        ObservableEventsEntryKind entryKind,
        Compilation compilation)
    {
        var result = new Dictionary<INamedTypeSymbol, EventInterfaceDescriptor>(SymbolEqualityComparer.Default);
        foreach (var type in seedTypes)
        {
            ExpandForInterfaces(type, result, entryKind, compilation);
        }

        ResolveInterfaceNameCollisions(result, entryKind);
        return result;
    }

    private static string GetEntryKindSourceTag(ObservableEventsEntryKind entryKind) =>
        entryKind switch
        {
            ObservableEventsEntryKind.FromEvents => "FromEvents",
            ObservableEventsEntryKind.FromEventHandlers => "FromEventHandlers",
            _ => throw new System.ArgumentOutOfRangeException(nameof(entryKind)),
        };

    private static string GetInterfaceNameSuffix(ObservableEventsEntryKind entryKind) =>
        entryKind switch
        {
            ObservableEventsEntryKind.FromEvents => "Events",
            ObservableEventsEntryKind.FromEventHandlers => "EventHandlers",
            _ => throw new System.ArgumentOutOfRangeException(nameof(entryKind)),
        };

    private static ImmutableArray<INamedTypeSymbol> ExpandForInterfaces(
        INamedTypeSymbol type,
        Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> result,
        ObservableEventsEntryKind entryKind,
        Compilation compilation)
    {
        if (result.ContainsKey(type))
        {
            return ImmutableArray.Create(type);
        }

        if (type.SpecialType == SpecialType.System_Object)
        {
            return ImmutableArray<INamedTypeSymbol>.Empty;
        }

        var parentTypes = new List<INamedTypeSymbol>();
        foreach (var parent in GetDirectBaseTypes(type))
        {
            var parentDef = parent.IsGenericType ? (INamedTypeSymbol)parent.OriginalDefinition : parent;
            var contribution = ExpandForInterfaces(parentDef, result, entryKind, compilation);
            foreach (var c in contribution)
            {
                if (!parentTypes.Contains(c, SymbolEqualityComparer.Default))
                {
                    parentTypes.Add(c);
                }
            }
        }

        var declaredEvents = type.GetMembers()
            .OfType<IEventSymbol>()
            .Where(e => e is
            {
                IsStatic: false,
                DeclaredAccessibility: Accessibility.Public,
                IsOverride: false,
            } && e.ExplicitInterfaceImplementations.IsEmpty)
            .ToList();

        var parentEventNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pt in parentTypes)
        {
            if (result.TryGetValue(pt, out var pd))
            {
                CollectAllInterfaceEventNames(pd, result, parentEventNames);
            }
        }

        var exclusiveEvents = declaredEvents
            .Where(e => !parentEventNames.Contains(e.Name))
            .OrderBy(static e => e.Name, StringComparer.Ordinal)
            .ToImmutableArray();

        if (exclusiveEvents.Length == 0 && parentTypes.Count == 0)
        {
            return ImmutableArray<INamedTypeSymbol>.Empty;
        }

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
        {
            yield return iface;
        }
    }

    private static void CollectAllInterfaceEventNames(
        EventInterfaceDescriptor descriptor,
        Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
        HashSet<string> names)
    {
        foreach (var evt in descriptor.ExclusiveEvents)
        {
            names.Add(evt.Name);
        }

        foreach (var parentType in descriptor.ParentTypes)
        {
            if (hierarchy.TryGetValue(parentType, out var pd))
            {
                CollectAllInterfaceEventNames(pd, hierarchy, names);
            }
        }
    }

    private static string ComputeRawEventInterfaceName(INamedTypeSymbol type, ObservableEventsEntryKind entryKind)
    {
        var suffix = GetInterfaceNameSuffix(entryKind);
        var name = type.Name;
        if (type.TypeKind == TypeKind.Interface && name.Length >= 2 && name[0] == 'I' && char.IsUpper(name[1]))
        {
            return $"{name}{suffix}";
        }

        return $"I{name}{suffix}";
    }

    private static void ResolveInterfaceNameCollisions(
        Dictionary<INamedTypeSymbol, EventInterfaceDescriptor> hierarchy,
        ObservableEventsEntryKind entryKind)
    {
        var suffix = GetInterfaceNameSuffix(entryKind);
        var byName = new Dictionary<string, List<INamedTypeSymbol>>(StringComparer.Ordinal);
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
            _ => throw new System.ArgumentOutOfRangeException(nameof(entryKind)),
        };
        return $"{type.Name}{suffix}";
    }

    private static TypeSyntax? GetEventInterfacePropertyType(
        IEventSymbol evt,
        ObservableEventsEntryKind entryKind,
        Compilation compilation)
    {
        if (evt.Type is not INamedTypeSymbol delegateType
            || delegateType.DelegateInvokeMethod is not IMethodSymbol invoke
            || !invoke.ReturnsVoid)
        {
            return null;
        }

        if (entryKind is ObservableEventsEntryKind.FromEvents)
        {
            return ObservableEventsSyntaxFactory.GetObservableReturnTypeSyntax(invoke.Parameters);
        }

        if (IsClassicSystemEventHandler(delegateType, compilation, out var genericEventArgs))
        {
            var eventArgsType = genericEventArgs is null
                ? SyntaxFactory.ParseTypeName("global::System.EventArgs")
                : SyntaxFactory.ParseTypeName(ObservableEventsConstants.QualifiedType(genericEventArgs));
            return ObservableEventsSyntaxFactory.ObservableSenderArgsTupleType(eventArgsType);
        }

        if (IsLegacySenderReceiverDelegate(delegateType, invoke, compilation))
        {
            return ObservableEventsSyntaxFactory.GetFromEventHandlersSenderReceiverReturnTypeSyntax(invoke.Parameters);
        }

        return null;
    }
}
