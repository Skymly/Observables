using Microsoft.CodeAnalysis;

namespace Observables.Events.R3.SourceGenerators;

internal static class ObservableEventsConstants
{
    internal const string BootstrapExtensionsMetadataName = "Observables.Events.R3.ObservableEventsBootstrapExtensions";
    internal const string GeneratedNamespace = "Observables.Events.R3";
    internal const string EventObservableMetadataName = "global::Observables.Events.R3.EventObservable";

    internal const string EventsEntryMethodName = "Events";
    internal const string EventHandlersEntryMethodName = "EventHandlers";

    internal const bool StaticObservableEventsGenerationEnabled = false;

    internal static readonly SymbolDisplayFormat FullyQualifiedNullableFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
            (SymbolDisplayMiscellaneousOptions)(1 << 6));

    internal static string QualifiedType(ITypeSymbol type) =>
        type.ToDisplayString(FullyQualifiedNullableFormat);
}
