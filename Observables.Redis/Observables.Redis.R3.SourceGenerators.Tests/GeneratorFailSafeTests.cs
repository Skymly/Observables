using Microsoft.CodeAnalysis;
using Observables.Redis.Generators;
using Observables.SourceGenerators.Shared;

namespace Observables.Redis.R3.SourceGenerators.Tests;

public sealed class GeneratorFailSafeTests
{
    [Fact]
    public void ExecuteParse_converts_exception_to_internal_diagnostic()
    {
        var (diagnostics, model) = GeneratorFailSafe.ExecuteParse(
            () => throw new InvalidOperationException("probe"),
            DiagnosticDescriptors.InternalGeneratorError,
            () => new ContextGenerationModel(ImmutableEquatableArray.Empty<RedisInterfaceModel>()));

        Assert.Single(diagnostics);
        Assert.Equal("OBS11008", diagnostics[0].Id);
        Assert.Contains("InvalidOperationException", diagnostics[0].GetMessage(), StringComparison.Ordinal);
        Assert.Contains("probe", diagnostics[0].GetMessage(), StringComparison.Ordinal);
        Assert.Empty(model.Interfaces);
    }

    [Fact]
    public void TryEmit_converts_exception_to_internal_diagnostic()
    {
        Diagnostic? reported = null;
        GeneratorFailSafe.TryEmit(
            () => throw new InvalidOperationException("emit probe"),
            d => reported = d,
            DiagnosticDescriptors.InternalGeneratorError);

        Assert.NotNull(reported);
        Assert.Equal("OBS11008", reported.Id);
        Assert.Contains("emit probe", reported.GetMessage(), StringComparison.Ordinal);
    }
}
