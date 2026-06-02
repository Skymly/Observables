using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Observables.RoutedEvents.Reactive.SourceGenerators;

internal readonly struct ObservableEventTargetSets
{
    public static readonly ObservableEventTargetSets Empty = new(
        ImmutableArray<INamedTypeSymbol>.Empty,
        ImmutableArray<INamedTypeSymbol>.Empty,
        ImmutableArray<INamedTypeSymbol>.Empty,
        ImmutableArray<INamedTypeSymbol>.Empty,
        ImmutableArray<GenericConstraintTarget>.Empty,
        ImmutableArray<GenericConstraintTarget>.Empty,
        ImmutableArray<AttachedRoutedEventTarget>.Empty,
        ImmutableArray<AttachedRoutedEventTarget>.Empty);

    public ObservableEventTargetSets(
        ImmutableArray<INamedTypeSymbol> eventsTypes,
        ImmutableArray<INamedTypeSymbol> eventHandlersTypes,
        ImmutableArray<INamedTypeSymbol> routedEventsTypes,
        ImmutableArray<INamedTypeSymbol> routedEventHandlersTypes,
        ImmutableArray<GenericConstraintTarget> eventsGenericConstraintTargets,
        ImmutableArray<GenericConstraintTarget> eventHandlersGenericConstraintTargets,
        ImmutableArray<AttachedRoutedEventTarget> attachedRoutedEventsTypes,
        ImmutableArray<AttachedRoutedEventTarget> attachedRoutedEventHandlersTypes)
    {
        EventsTypes = eventsTypes;
        EventHandlersTypes = eventHandlersTypes;
        RoutedEventsTypes = routedEventsTypes;
        RoutedEventHandlersTypes = routedEventHandlersTypes;
        EventsGenericConstraintTargets = eventsGenericConstraintTargets;
        EventHandlersGenericConstraintTargets = eventHandlersGenericConstraintTargets;
        AttachedRoutedEventsTypes = attachedRoutedEventsTypes;
        AttachedRoutedEventHandlersTypes = attachedRoutedEventHandlersTypes;
    }

    public ImmutableArray<INamedTypeSymbol> EventsTypes { get; }
    public ImmutableArray<INamedTypeSymbol> EventHandlersTypes { get; }
    public ImmutableArray<INamedTypeSymbol> RoutedEventsTypes { get; }
    public ImmutableArray<INamedTypeSymbol> RoutedEventHandlersTypes { get; }
    public ImmutableArray<GenericConstraintTarget> EventsGenericConstraintTargets { get; }
    public ImmutableArray<GenericConstraintTarget> EventHandlersGenericConstraintTargets { get; }
    public ImmutableArray<AttachedRoutedEventTarget> AttachedRoutedEventsTypes { get; }
    public ImmutableArray<AttachedRoutedEventTarget> AttachedRoutedEventHandlersTypes { get; }
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

internal readonly struct AttachedRoutedEventTarget
{
    public AttachedRoutedEventTarget(INamedTypeSymbol receiverType)
    {
        ReceiverType = receiverType;
    }

    public INamedTypeSymbol ReceiverType { get; }
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
