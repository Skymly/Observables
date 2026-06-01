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
        ImmutableArray<INamedTypeSymbol> fromEventsTypes,
        ImmutableArray<INamedTypeSymbol> fromEventHandlersTypes,
        ImmutableArray<INamedTypeSymbol> fromRoutedEventsTypes,
        ImmutableArray<INamedTypeSymbol> fromRoutedEventHandlersTypes,
        ImmutableArray<GenericConstraintTarget> fromEventsGenericConstraintTargets,
        ImmutableArray<GenericConstraintTarget> fromEventHandlersGenericConstraintTargets,
        ImmutableArray<AttachedRoutedEventTarget> fromAttachedRoutedEventsTypes,
        ImmutableArray<AttachedRoutedEventTarget> fromAttachedRoutedEventHandlersTypes)
    {
        FromEventsTypes = fromEventsTypes;
        FromEventHandlersTypes = fromEventHandlersTypes;
        FromRoutedEventsTypes = fromRoutedEventsTypes;
        FromRoutedEventHandlersTypes = fromRoutedEventHandlersTypes;
        FromEventsGenericConstraintTargets = fromEventsGenericConstraintTargets;
        FromEventHandlersGenericConstraintTargets = fromEventHandlersGenericConstraintTargets;
        FromAttachedRoutedEventsTypes = fromAttachedRoutedEventsTypes;
        FromAttachedRoutedEventHandlersTypes = fromAttachedRoutedEventHandlersTypes;
    }

    public ImmutableArray<INamedTypeSymbol> FromEventsTypes { get; }
    public ImmutableArray<INamedTypeSymbol> FromEventHandlersTypes { get; }
    public ImmutableArray<INamedTypeSymbol> FromRoutedEventsTypes { get; }
    public ImmutableArray<INamedTypeSymbol> FromRoutedEventHandlersTypes { get; }
    public ImmutableArray<GenericConstraintTarget> FromEventsGenericConstraintTargets { get; }
    public ImmutableArray<GenericConstraintTarget> FromEventHandlersGenericConstraintTargets { get; }
    public ImmutableArray<AttachedRoutedEventTarget> FromAttachedRoutedEventsTypes { get; }
    public ImmutableArray<AttachedRoutedEventTarget> FromAttachedRoutedEventHandlersTypes { get; }
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
