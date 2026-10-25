using Microsoft.CodeAnalysis;

namespace Observables.Analyzers.Tests;

public sealed class ReactivePackageConflictAnalyzerTests
{
    const string MarkerSource = """
        namespace Test;

        public interface IMarker { }
        """;

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
    public void Every_conflict_domain_declares_three_backend_package_ids()
    {
        foreach (var domain in ProxyDomainCatalog.ReactiveConflictDomains)
        {
            Assert.Equal(
                [
                    $"Observables.{domain.Definition.DisplayName}.R3",
                    $"Observables.{domain.Definition.DisplayName}.Reactive",
                    $"Observables.{domain.Definition.DisplayName}.Primitives",
                ],
                domain.Definition.BackendPackageIds.ToArray());
        }
    }

    [Fact]
    public void OBS0001_when_signalr_r3_and_reactive_bridges_are_referenced()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            MarkerSource,
            additionalReferences:
            [
                AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(global::Observables.SignalR.HubAttribute)),
                AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(global::Observables.SignalR.SignalRObservable)),
                AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(global::Observables.SignalR.Reactive.SystemReactiveSignalRAdapter)),
            ],
            new ReactivePackageConflictAnalyzer());

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "OBS0001");
        var message = diagnostic.GetMessage();
        Assert.Contains("SignalR", message, StringComparison.Ordinal);
        Assert.Contains("Observables.SignalR.R3", message, StringComparison.Ordinal);
        Assert.Contains("Observables.SignalR.Reactive", message, StringComparison.Ordinal);
    }

    [Fact]
    public void OBS0001_when_restapi_r3_marker_and_reactive_bridge_are_present()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            """
            namespace Observables.RestAPI.Generated
            {
                internal static class R3BackendMarker { }
            }
            """,
            additionalReferences:
            [
                AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(global::Observables.RestAPI.RestApiAttribute)),
                AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(global::Observables.RestAPI.Reactive.SystemReactiveObservableAdapter)),
            ],
            new ReactivePackageConflictAnalyzer());

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "OBS0001");
        Assert.Contains("Observables.RestAPI.R3", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void OBS0001_when_restapi_both_backend_markers_are_present()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            """
            namespace Observables.RestAPI.Generated
            {
                internal static class R3BackendMarker { }
            }

            namespace Observables.RestAPI.Reactive.Generated
            {
                internal static class ReactiveBackendMarker { }
            }
            """,
            additionalReferences:
                [AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(global::Observables.RestAPI.RestApiAttribute))],
            new ReactivePackageConflictAnalyzer());

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "OBS0001");
        var message = diagnostic.GetMessage();
        Assert.Contains("Observables.RestAPI.R3", message, StringComparison.Ordinal);
        Assert.Contains("Observables.RestAPI.Reactive", message, StringComparison.Ordinal);
    }

    [Fact]
    public void OBS0001_when_events_both_bootstrap_markers_are_present()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            """
            namespace Observables.Events.R3
            {
                internal static class ObservableEventsBootstrapExtensions { }
            }

            namespace Observables.Events.Reactive
            {
                internal static class ObservableEventsBootstrapExtensions { }
            }
            """,
            additionalReferences: [],
            new ReactivePackageConflictAnalyzer());

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "OBS0001");
        var message = diagnostic.GetMessage();
        Assert.Contains("Observables.Events.R3", message, StringComparison.Ordinal);
        Assert.Contains("Observables.Events.Reactive", message, StringComparison.Ordinal);
    }

    [Fact]
    public void No_OBS0001_when_only_events_r3_bootstrap_marker_is_present()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            """
            namespace Observables.Events.R3
            {
                internal static class ObservableEventsBootstrapExtensions { }
            }
            """,
            additionalReferences: [],
            new ReactivePackageConflictAnalyzer());

        Assert.DoesNotContain(diagnostics, d => d.Id == "OBS0001");
    }

    [Fact]
    public void OBS0001_when_signalr_r3_and_reactive_backend_keys_are_declared()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            MarkerSource,
            additionalReferences: [],
            globalOptions: new Dictionary<string, string>
            {
                ["build_property.ObservablesSignalRBackend_R3"] = "true",
                ["build_property.ObservablesSignalRBackend_Reactive"] = "true",
            },
            new ReactivePackageConflictAnalyzer());

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "OBS0001");
        var message = diagnostic.GetMessage();
        Assert.Contains("Observables.SignalR.R3", message, StringComparison.Ordinal);
        Assert.Contains("Observables.SignalR.Reactive", message, StringComparison.Ordinal);
    }

    [Fact]
    public void OBS0001_when_restapi_backend_keys_are_declared()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            MarkerSource,
            additionalReferences: [],
            globalOptions: new Dictionary<string, string>
            {
                ["build_property.ObservablesRestAPIBackend_R3"] = "true",
                ["build_property.ObservablesRestAPIBackend_Reactive"] = "true",
            },
            new ReactivePackageConflictAnalyzer());

        Assert.Single(diagnostics, d => d.Id == "OBS0001");
    }

    [Fact]
    public void OBS0001_single_diagnostic_lists_all_three_backends_when_three_keys_declared()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            MarkerSource,
            additionalReferences: [],
            globalOptions: new Dictionary<string, string>
            {
                ["build_property.ObservablesMqttBackend_R3"] = "true",
                ["build_property.ObservablesMqttBackend_Reactive"] = "true",
                ["build_property.ObservablesMqttBackend_Primitives"] = "true",
            },
            new ReactivePackageConflictAnalyzer());

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "OBS0001");
        var message = diagnostic.GetMessage();
        Assert.Contains("Observables.Mqtt.R3", message, StringComparison.Ordinal);
        Assert.Contains("Observables.Mqtt.Reactive", message, StringComparison.Ordinal);
        Assert.Contains("Observables.Mqtt.Primitives", message, StringComparison.Ordinal);
    }

    [Fact]
    public void OBS0001_when_r3_and_primitives_keys_are_declared_without_reactive()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            MarkerSource,
            additionalReferences: [],
            globalOptions: new Dictionary<string, string>
            {
                ["build_property.ObservablesRedisBackend_R3"] = "true",
                ["build_property.ObservablesRedisBackend_Primitives"] = "true",
            },
            new ReactivePackageConflictAnalyzer());

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "OBS0001");
        var message = diagnostic.GetMessage();
        Assert.Contains("Observables.Redis.R3", message, StringComparison.Ordinal);
        Assert.Contains("Observables.Redis.Primitives", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Observables.Redis.Reactive", message, StringComparison.Ordinal);
    }

    [Fact]
    public void OBS0001_when_declared_key_and_symbol_probe_hit_different_backends()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            MarkerSource,
            additionalReferences:
                [AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(global::Observables.SignalR.Reactive.SystemReactiveSignalRAdapter))],
            globalOptions: new Dictionary<string, string>
            {
                ["build_property.ObservablesSignalRBackend_R3"] = "true",
            },
            new ReactivePackageConflictAnalyzer());

        Assert.Single(diagnostics, d => d.Id == "OBS0001");
    }

    [Fact]
    public void No_OBS0001_when_declared_key_and_symbol_probe_hit_the_same_backend()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            MarkerSource,
            additionalReferences:
                [AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(global::Observables.SignalR.SignalRObservable))],
            globalOptions: new Dictionary<string, string>
            {
                ["build_property.ObservablesSignalRBackend_R3"] = "true",
            },
            new ReactivePackageConflictAnalyzer());

        Assert.DoesNotContain(diagnostics, d => d.Id == "OBS0001");
    }

    [Fact]
    public void No_OBS0001_when_only_a_single_backend_key_is_declared()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            MarkerSource,
            additionalReferences: [],
            globalOptions: new Dictionary<string, string>
            {
                ["build_property.ObservablesGrpcBackend_R3"] = "true",
            },
            new ReactivePackageConflictAnalyzer());

        Assert.DoesNotContain(diagnostics, d => d.Id == "OBS0001");
    }

    [Fact]
    public void No_OBS0001_when_backend_key_is_declared_false()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            MarkerSource,
            additionalReferences: [],
            globalOptions: new Dictionary<string, string>
            {
                ["build_property.ObservablesSseBackend_R3"] = "false",
                ["build_property.ObservablesSseBackend_Reactive"] = "true",
            },
            new ReactivePackageConflictAnalyzer());

        Assert.DoesNotContain(diagnostics, d => d.Id == "OBS0001");
    }

    [Fact]
    public void No_OBS0001_when_r3_library_and_signalr_reactive_are_referenced()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            MarkerSource,
            additionalReferences:
            [
                AnalyzerTestHarness.CreateReference<global::R3.Unit>(),
                AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(global::Observables.SignalR.HubAttribute)),
                AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(global::Observables.SignalR.Reactive.SystemReactiveSignalRAdapter)),
            ],
            new ReactivePackageConflictAnalyzer());

        Assert.DoesNotContain(diagnostics, d => d.Id == "OBS0001");
    }

    [Fact]
    public void No_OBS0001_when_r3_library_and_redis_reactive_are_referenced()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            MarkerSource,
            additionalReferences:
            [
                AnalyzerTestHarness.CreateReference<global::R3.Unit>(),
                AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(global::Observables.Redis.RedisAttribute)),
                AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(global::Observables.Redis.Reactive.SystemReactiveRedisAdapter)),
            ],
            new ReactivePackageConflictAnalyzer());

        Assert.DoesNotContain(diagnostics, d => d.Id == "OBS0001");
    }

    [Fact]
    public void No_OBS0001_when_r3_library_and_sse_reactive_are_referenced()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            MarkerSource,
            additionalReferences:
            [
                AnalyzerTestHarness.CreateReference<global::R3.Unit>(),
                AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(global::Observables.Sse.SseAttribute)),
                AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(global::Observables.Sse.Reactive.SystemReactiveSseAdapter)),
            ],
            new ReactivePackageConflictAnalyzer());

        Assert.DoesNotContain(diagnostics, d => d.Id == "OBS0001");
    }

    [Fact]
    public void No_OBS0001_when_only_r3_is_referenced()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            MarkerSource,
            additionalReferences: [AnalyzerTestHarness.CreateReference<global::R3.Unit>()],
            new ReactivePackageConflictAnalyzer());

        Assert.DoesNotContain(diagnostics, d => d.Id == "OBS0001");
    }

    [Fact]
    public void No_OBS0001_when_neither_r3_nor_reactive_bridge_is_referenced()
    {
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            MarkerSource,
            new ReactivePackageConflictAnalyzer());

        Assert.DoesNotContain(diagnostics, d => d.Id == "OBS0001");
    }
}
