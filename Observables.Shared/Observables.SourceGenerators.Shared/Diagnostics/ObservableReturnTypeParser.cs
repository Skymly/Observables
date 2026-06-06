using Microsoft.CodeAnalysis;

namespace Observables.SourceGenerators.Shared.Diagnostics;

/// <summary>Shared Observable / IObservable return-type validation for interface-proxy generators.</summary>
public static class ObservableReturnTypeParser
{
    static readonly SymbolDisplayFormat DisplayFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    /// Validates return type against the active generator backend (R3 vs System.Reactive).
    /// </summary>
    /// <param name="isR3Generator">True when compiling the R3 source generator (SIGNALR_R3, MQTT_R3, etc.).</param>
    /// <param name="reactiveAdapterMetadataName">
    /// Fully qualified metadata name of the Reactive bridge type, e.g.
    /// <c>Observables.SignalR.Reactive.SystemReactiveSignalRAdapter</c>.
    /// </param>
    public static bool TryParse(
        ITypeSymbol returnType,
        Compilation compilation,
        bool isR3Generator,
        string reactiveAdapterMetadataName,
        INamedTypeSymbol? expectedObservableType,
        INamedTypeSymbol? unitType,
        bool requiresUnitPayload,
        DiagnosticDescriptor unsupportedReturnType,
        DiagnosticDescriptor systemReactiveNotReferenced,
        Location? location,
        List<Diagnostic> diagnostics,
        out string resultTypeDisplay,
        out string returnTypeDisplay)
    {
        resultTypeDisplay = string.Empty;
        returnTypeDisplay = returnType.ToDisplayString(DisplayFormat);
        location ??= Location.None;

        if (returnType is not INamedTypeSymbol named || !named.IsGenericType)
        {
            diagnostics.Add(Diagnostic.Create(unsupportedReturnType, location, returnTypeDisplay));
            return false;
        }

        var def = named.OriginalDefinition;
        var metadata = def.MetadataName;
        var ns = def.ContainingNamespace?.ToDisplayString();

        if (metadata == "IObservable`1" && ns == "System")
        {
            if (isR3Generator)
            {
                diagnostics.Add(
                    Diagnostic.Create(systemReactiveNotReferenced, location, returnTypeDisplay));
                return false;
            }

            if (compilation.GetTypeByMetadataName(reactiveAdapterMetadataName) is null)
            {
                diagnostics.Add(
                    Diagnostic.Create(systemReactiveNotReferenced, location, returnTypeDisplay));
                return false;
            }
        }
        else if (metadata == "Observable`1" && ns == "R3")
        {
            if (!isR3Generator)
            {
                diagnostics.Add(Diagnostic.Create(unsupportedReturnType, location, returnTypeDisplay));
                return false;
            }
        }
        else if (expectedObservableType is null
                 || !SymbolEqualityComparer.Default.Equals(def, expectedObservableType))
        {
            diagnostics.Add(Diagnostic.Create(unsupportedReturnType, location, returnTypeDisplay));
            return false;
        }

        if (named.TypeArguments.Length != 1)
        {
            diagnostics.Add(Diagnostic.Create(unsupportedReturnType, location, returnTypeDisplay));
            return false;
        }

        resultTypeDisplay = named.TypeArguments[0].ToDisplayString(DisplayFormat);

        if (requiresUnitPayload)
        {
            if (unitType is null
                || !SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], unitType))
            {
                diagnostics.Add(Diagnostic.Create(unsupportedReturnType, location, returnTypeDisplay));
                return false;
            }
        }

        return true;
    }
}
