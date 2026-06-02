using Microsoft.CodeAnalysis;

namespace Observables.Events.Reactive.SourceGenerators;

internal static class ObservableEventsConstants
{
    internal const string BootstrapExtensionsMetadataName = "Observables.Events.Reactive.ObservableEventsBootstrapExtensions";
    internal const string GeneratedNamespace = "Observables.Events.Reactive";
    internal const string EventObservableMetadataName = "global::Observables.Events.Reactive.EventObservable";

    /// <summary>
    /// Entry name: instance <c>source.Events()</c> (extension method under <c>R3.SourceGenerators</c>); static <c>ObservableEventsStatics.OBS_* .Events</c> (property). Per-event streams are properties (ReactiveMarbles-style).
    /// </summary>
    internal const string EventsEntryMethodName = "Events";

    /// <summary>
    /// Entry name: instance <c>source.EventHandlers()</c> (uses <c>EventObservable.EventHandler</c>).
    /// </summary>
    internal const string EventHandlersEntryMethodName = "EventHandlers";

    /// <summary>
    /// Entry name: instance <c>source.RoutedEvents()</c> — WPF (<c>UseWPF</c>) only; emits streams for CLR instance events backed by a <c>RoutedEvent</c> field (<c>{EventName}Event</c>).
    /// </summary>
    internal const string RoutedEventsEntryMethodName = "RoutedEvents";

    /// <summary>
    /// Entry name: instance <c>source.RoutedEventHandlers()</c> — WPF only; same routed-event filter as <see cref="RoutedEventsEntryMethodName"/> with <c>EventObservable.EventHandler</c>.
    /// </summary>
    internal const string RoutedEventHandlersEntryMethodName = "RoutedEventHandlers";

    internal const string AttachedRoutedEventEntryMethodName = "AttachedRoutedEvent";
    internal const string AttachedRoutedEventHandlerEntryMethodName = "AttachedRoutedEventHandler";

    /// <summary>
    /// When <see langword="false"/>, no <c>ObservableEventsStatics</c> / <c>OBS_*</c> / static-event wrappers are emitted and static <c>Events</c> member accesses are not discovered.
    /// </summary>
    internal const bool StaticObservableEventsGenerationEnabled = false;

    /// <summary>
    /// Same as <see cref="SymbolDisplayFormat.FullyQualifiedFormat"/>, plus NRT <c>?</c> so emitted types match delegate/event signatures (<c>IncludeNullableReferenceTypeModifier</c>, <c>1 &lt;&lt; 6</c>; see dotnet/roslyn <c>SymbolDisplayMiscellaneousOptions</c> — not always present on older netstandard2 reference assemblies, so bitmask is spelled out).
    /// </summary>
    internal static readonly SymbolDisplayFormat FullyQualifiedNullableFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
            (SymbolDisplayMiscellaneousOptions)(1 << 6));

    internal static string QualifiedType(ITypeSymbol type) =>
        type.ToDisplayString(FullyQualifiedNullableFormat);
}
