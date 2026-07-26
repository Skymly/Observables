namespace Observables.SourceGenerators.Shared;

/// <summary>
/// Compile-time R3 vs System.Reactive tokens for IO proxy generators.
/// Driven by <c>OBSERVABLES_R3</c> from <c>Observables.SourceGenerators.R3.props</c>.
/// Domain-specific BridgeType / adapter metadata stay in each domain Parser/Emitter.
/// </summary>
internal static class BackendTokens
{
#if OBSERVABLES_R3
    public const string ObservableMetadataName = "R3.Observable`1";
    public const string UnitMetadataName = "R3.Unit";
    public const bool IsR3 = true;
#else
    public const string ObservableMetadataName = "System.IObservable`1";
    public const string UnitMetadataName = "System.Reactive.Unit";
    public const bool IsR3 = false;
#endif

    /// <summary>
    /// Qualifies the generated proxy namespace for an IO domain root
    /// (e.g. <c>Observables.SignalR</c> → <c>...Generated</c> or <c>...Reactive.Generated</c>).
    /// </summary>
    public static string QualifyGeneratedNamespace(string domainRoot) =>
#if OBSERVABLES_R3
        domainRoot + ".Generated";
#else
        domainRoot + ".Reactive.Generated";
#endif
}
