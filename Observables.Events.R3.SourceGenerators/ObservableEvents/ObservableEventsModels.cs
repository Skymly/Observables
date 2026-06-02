using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Observables.Events.R3.SourceGenerators;

internal readonly struct ObservableEventTargetSets
{
    public static readonly ObservableEventTargetSets Empty = new(
        ImmutableArray<INamedTypeSymbol>.Empty,
        ImmutableArray<INamedTypeSymbol>.Empty,
        ImmutableArray<GenericConstraintTarget>.Empty,
        ImmutableArray<GenericConstraintTarget>.Empty);

    public ObservableEventTargetSets(
        ImmutableArray<INamedTypeSymbol> eventsTypes,
        ImmutableArray<INamedTypeSymbol> eventHandlersTypes,
        ImmutableArray<GenericConstraintTarget> eventsGenericConstraintTargets,
        ImmutableArray<GenericConstraintTarget> eventHandlersGenericConstraintTargets)
    {
        EventsTypes = eventsTypes;
        EventHandlersTypes = eventHandlersTypes;
        EventsGenericConstraintTargets = eventsGenericConstraintTargets;
        EventHandlersGenericConstraintTargets = eventHandlersGenericConstraintTargets;
    }

    public ImmutableArray<INamedTypeSymbol> EventsTypes { get; }
    public ImmutableArray<INamedTypeSymbol> EventHandlersTypes { get; }
    public ImmutableArray<GenericConstraintTarget> EventsGenericConstraintTargets { get; }
    public ImmutableArray<GenericConstraintTarget> EventHandlersGenericConstraintTargets { get; }
}

internal readonly struct GenericConstraintTarget
{
    public GenericConstraintTarget(ImmutableArray<INamedTypeSymbol> constraintTypes)
    {
        ConstraintTypes = constraintTypes;
        Key = string.Join(
            "__",
            constraintTypes.Select(static t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
    }

    public ImmutableArray<INamedTypeSymbol> ConstraintTypes { get; }
    public string Key { get; }
}

internal sealed class EventInterfaceDescriptor
{
    public EventInterfaceDescriptor(
        INamedTypeSymbol sourceType,
        string interfaceName,
        ImmutableArray<IEventSymbol> exclusiveEvents,
        ImmutableArray<INamedTypeSymbol> parentTypes)
    {
        SourceType = sourceType;
        InterfaceName = interfaceName;
        ExclusiveEvents = exclusiveEvents;
        ParentTypes = parentTypes;
    }

    public INamedTypeSymbol SourceType { get; }
    public string InterfaceName { get; set; }
    public ImmutableArray<IEventSymbol> ExclusiveEvents { get; }
    public ImmutableArray<INamedTypeSymbol> ParentTypes { get; }
}
