using Microsoft.CodeAnalysis;

namespace Observables.SignalR.Reactive.SourceGenerators.Tests;

public sealed class GeneratorFailSafeTests
{
    [Fact]
    public void Hub_generator_reports_OBS4008_instead_of_crashing_on_internal_error()
    {
        const string userSource =
            """
            [Hub]
            public interface IInternalErrorProbe
            {
                [HubInvoke]
                IObservable<int> Ping();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource, failSafeProbe: true);

        Assert.Contains(
            output.Diagnostics,
            d => d.Id == "OBS4008"
                && d.GetMessage().Contains("fail-safe probe", StringComparison.Ordinal));
        Assert.DoesNotContain(output.GeneratedSources, s => s.HintName.Contains("IInternalErrorProbe"));
    }

    [Fact]
    public void Hub_generator_does_not_treat_IInternalErrorProbe_as_fail_safe_without_build_property()
    {
        const string userSource =
            """
            [Hub]
            public interface IInternalErrorProbe
            {
                [HubInvoke]
                IObservable<int> Ping();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);

        Assert.DoesNotContain(
            output.Diagnostics,
            d => d.Id == "OBS4008"
                && d.GetMessage().Contains("fail-safe probe", StringComparison.Ordinal));
        Assert.Contains(output.GeneratedSources, s => s.HintName.Contains("IInternalErrorProbe"));
    }
}
