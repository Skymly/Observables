using Microsoft.CodeAnalysis;

namespace Observables.RoutedEvents.Reactive.SourceGenerators;

internal static class ObservableEventsConstants
{
    internal const string BootstrapExtensionsMetadataName = "Observables.RoutedEvents.Reactive.ObservableEventsBootstrapExtensions";
    internal const string GeneratedNamespace = "Observables.RoutedEvents.Reactive";
    internal const string EventObservableMetadataName = "global::Observables.RoutedEvents.Reactive.EventObservable";

    /// <summary>
    /// Entry name: instance <c>source.Events()</c> (extension method); static <c>ObservableEventsStatics.OBS_* .Events</c> (property).
    /// </summary>
    internal const string EventsEntryMethodName = "Events";

    /// <summary>
    /// Entry name: instance <c>source.EventHandlers()</c> (uses System.Reactive event handler bridging).
    /// </summary>
    internal const string EventHandlersEntryMethodName = "EventHandlers";

    /// <summary>
    /// Entry name: instance <c>source.RoutedEvents()</c> — WPF (<c>UseWPF</c>) / Avalonia routed CLR events.
    /// </summary>
    internal const string RoutedEventsEntryMethodName = "RoutedEvents";

    /// <summary>
    /// Entry name: instance <c>source.RoutedEventHandlers()</c>; same routed-event filter as <see cref="RoutedEventsEntryMethodName"/>.
    /// </summary>
    internal const string RoutedEventHandlersEntryMethodName = "RoutedEventHandlers";

    internal const string AttachedRoutedEventEntryMethodName = "AttachedRoutedEvent";
    internal const string AttachedRoutedEventHandlerEntryMethodName = "AttachedRoutedEventHandler";

    /// <summary>
    /// When <see langword="false"/>, no <c>ObservableEventsStatics</c> / <c>OBS_*</c> / static-event wrappers are emitted and static <c>Events</c> member accesses are not discovered.
    /// </summary>
    internal const bool StaticObservableEventsGenerationEnabled = false;

    internal static readonly SymbolDisplayFormat FullyQualifiedNullableFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
            (SymbolDisplayMiscellaneousOptions)(1 << 6));

    internal static string QualifiedType(ITypeSymbol type) =>
        type.ToDisplayString(FullyQualifiedNullableFormat);
}
