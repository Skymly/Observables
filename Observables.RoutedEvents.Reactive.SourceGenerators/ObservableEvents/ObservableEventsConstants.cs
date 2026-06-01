using Microsoft.CodeAnalysis;

namespace Observables.RoutedEvents.Reactive.SourceGenerators;

internal static class ObservableEventsConstants
{
    internal const string BootstrapExtensionsMetadataName = "Observables.RoutedEvents.Reactive.ObservableEventsBootstrapExtensions";
    internal const string GeneratedNamespace = "Observables.RoutedEvents.Reactive";

    /// <summary>
    /// Entry name: instance <c>source.FromEvents()</c> (extension method under <c>R3.SourceGenerators</c>); static <c>ObservableEventsStatics.OBS_* .FromEvents</c> (property). Per-event streams are properties (ReactiveMarbles-style).
    /// </summary>
    internal const string FromEventsEntryMethodName = "FromEvents";

    /// <summary>
    /// Entry name: instance <c>source.FromEventHandlers()</c> (uses <c>R3.Observable.FromEventHandler</c>).
    /// </summary>
    internal const string FromEventHandlersEntryMethodName = "FromEventHandlers";

    /// <summary>
    /// Entry name: instance <c>source.FromRoutedEvents()</c> — WPF (<c>UseWPF</c>) only; emits streams for CLR instance events backed by a <c>RoutedEvent</c> field (<c>{EventName}Event</c>).
    /// </summary>
    internal const string FromRoutedEventsEntryMethodName = "FromRoutedEvents";

    /// <summary>
    /// Entry name: instance <c>source.FromRoutedEventHandlers()</c> — WPF only; same routed-event filter as <see cref="FromRoutedEventsEntryMethodName"/> with <c>R3.Observable.FromEventHandler</c>.
    /// </summary>
    internal const string FromRoutedEventHandlersEntryMethodName = "FromRoutedEventHandlers";

    internal const string FromAttachedRoutedEventEntryMethodName = "FromAttachedRoutedEvent";
    internal const string FromAttachedRoutedEventHandlerEntryMethodName = "FromAttachedRoutedEventHandler";

    /// <summary>
    /// When <see langword="false"/>, no <c>ObservableEventsStatics</c> / <c>OBS_*</c> / static-event wrappers are emitted and static <c>FromEvents</c> member accesses are not discovered.
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
