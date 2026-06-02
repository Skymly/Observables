using Microsoft.CodeAnalysis;

namespace Observables.Events.Reactive.SourceGenerators;

internal static class ObservableEventsConstants
{
    internal const string BootstrapExtensionsMetadataName = "Observables.Events.Reactive.ObservableEventsBootstrapExtensions";
    internal const string GeneratedNamespace = "Observables.Events.Reactive";
    internal const string EventObservableMetadataName = "global::Observables.Events.Reactive.EventObservable";

    internal const string EventsEntryMethodName = "Events";
    internal const string EventHandlersEntryMethodName = "EventHandlers";

    internal const bool StaticObservableEventsGenerationEnabled = false;

    internal static readonly SymbolDisplayFormat FullyQualifiedNullableFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
            (SymbolDisplayMiscellaneousOptions)(1 << 6));

    internal static string QualifiedType(ITypeSymbol type) =>
        type.ToDisplayString(FullyQualifiedNullableFormat);
}
