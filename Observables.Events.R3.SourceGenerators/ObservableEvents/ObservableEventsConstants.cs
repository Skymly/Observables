using Microsoft.CodeAnalysis;

namespace Observables.Events.R3.SourceGenerators;

internal static class ObservableEventsConstants
{
    internal const string BootstrapExtensionsMetadataName = "Observables.Events.R3.ObservableEventsBootstrapExtensions";
    internal const string GeneratedNamespace = "Observables.Events.R3";

    internal const string FromEventsEntryMethodName = "FromEvents";
    internal const string FromEventHandlersEntryMethodName = "FromEventHandlers";

    internal const bool StaticObservableEventsGenerationEnabled = false;

    internal static readonly SymbolDisplayFormat FullyQualifiedNullableFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
            (SymbolDisplayMiscellaneousOptions)(1 << 6));

    internal static string QualifiedType(ITypeSymbol type) =>
        type.ToDisplayString(FullyQualifiedNullableFormat);
}
