using Microsoft.CodeAnalysis;
using Observables.SignalR.Generators;
using Observables.SourceGenerators.Shared;

namespace Observables.SignalR.R3.SourceGenerators.Tests;

public sealed class GeneratorFailSafeTests
{
    [Fact]
    public void ExecuteParse_converts_exception_to_internal_diagnostic()
    {
        var (diagnostics, model) = GeneratorFailSafe.ExecuteParse(
            () => throw new InvalidOperationException("probe"),
            DiagnosticDescriptors.InternalGeneratorError,
            () => new ContextGenerationModel(ImmutableEquatableArray.Empty<HubInterfaceModel>()));

        Assert.Single(diagnostics);
        Assert.Equal("OBS4008", diagnostics[0].Id);
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
        Assert.Equal("OBS4008", reported.Id);
        Assert.Contains("emit probe", reported.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void Hub_generator_reports_OBS4008_instead_of_crashing_on_internal_error()
    {
        const string userSource =
            """
            [Hub]
            public interface IInternalErrorProbe
            {
                [HubInvoke]
                Observable<int> Ping();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);

        Assert.Contains(
            output.Diagnostics,
            d => d.Id == "OBS4008"
                && d.GetMessage().Contains("fail-safe probe", StringComparison.Ordinal));
        Assert.DoesNotContain(output.GeneratedSources, s => s.HintName.Contains("IInternalErrorProbe"));
    }
}
