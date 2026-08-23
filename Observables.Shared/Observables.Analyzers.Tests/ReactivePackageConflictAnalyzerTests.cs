using Microsoft.CodeAnalysis;

namespace Observables.Analyzers.Tests;

public sealed class ReactivePackageConflictAnalyzerTests
{
    [Fact]
    public void ReactiveAssemblyName_matches_Observables_DisplayName_Reactive_for_all_conflict_domains()
    {
        foreach (var domain in ProxyDomainCatalog.ReactiveConflictDomains)
        {
            Assert.Equal(
                $"Observables.{domain.Definition.DisplayName}.Reactive",
                domain.Definition.ReactiveAssemblyName);
        }
    }

    [Fact]
    public void OBS0001_when_r3_and_signalr_reactive_are_referenced()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            """
            namespace Test;

            public interface IMarker { }
            """,
            additionalReferences:
            [
                AnalyzerTestHarness.CreateReference<global::R3.Unit>(),
                AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(global::Observables.SignalR.HubAttribute)),
                AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(global::Observables.SignalR.Reactive.SystemReactiveSignalRAdapter)),
            ],
            new ReactivePackageConflictAnalyzer());

        Assert.Contains(
            diagnostics,
            d => d.Id == "OBS0001" && d.GetMessage().Contains("SignalR", StringComparison.Ordinal));
    }

    [Fact]
    public void OBS0001_when_r3_and_redis_reactive_are_referenced()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            """
            namespace Test;

            public interface IMarker { }
            """,
            additionalReferences:
            [
                AnalyzerTestHarness.CreateReference<global::R3.Unit>(),
                AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(global::Observables.Redis.RedisAttribute)),
                AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(global::Observables.Redis.Reactive.SystemReactiveRedisAdapter)),
            ],
            new ReactivePackageConflictAnalyzer());

        Assert.Contains(
            diagnostics,
            d => d.Id == "OBS0001" && d.GetMessage().Contains("Redis", StringComparison.Ordinal));
    }

    [Fact]
    public void OBS0001_when_r3_and_sse_reactive_are_referenced()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            """
            namespace Test;

            public interface IMarker { }
            """,
            additionalReferences:
            [
                AnalyzerTestHarness.CreateReference<global::R3.Unit>(),
                AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(global::Observables.Sse.SseAttribute)),
                AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(global::Observables.Sse.Reactive.SystemReactiveSseAdapter)),
            ],
            new ReactivePackageConflictAnalyzer());

        Assert.Contains(
            diagnostics,
            d => d.Id == "OBS0001" && d.GetMessage().Contains("Sse", StringComparison.Ordinal));
    }

    [Fact]
    public void No_OBS0001_when_only_r3_is_referenced()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            """
            namespace Test;

            public interface IMarker { }
            """,
            additionalReferences: [AnalyzerTestHarness.CreateReference<global::R3.Unit>()],
            new ReactivePackageConflictAnalyzer());

        Assert.DoesNotContain(diagnostics, d => d.Id == "OBS0001");
    }

    [Fact]
    public void No_OBS0001_when_neither_r3_nor_reactive_bridge_is_referenced()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            """
            namespace Test;

            public interface IMarker { }
            """,
            new ReactivePackageConflictAnalyzer());

        Assert.DoesNotContain(diagnostics, d => d.Id == "OBS0001");
    }
}
