namespace Observables.CodeFixes.Tests;

public sealed class ObservablesPackageReferenceMappingsTests
{
    [Fact]
    public void RuntimePackageByDiagnosticId_covers_core_not_referenced_diagnostics()
    {
        Assert.Equal(
            ["OBS10002", "OBS11002", "OBS3002", "OBS4002", "OBS5002", "OBS6002", "OBS7002", "OBS8002", "OBS9002"],
            ObservablesPackageReferenceMappings.RuntimePackageByDiagnosticId.Keys.OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public void ReactivePackageByDiagnosticId_covers_reactive_package_diagnostics()
    {
        Assert.Equal(
            ["OBS10005", "OBS11005", "OBS3005", "OBS4005", "OBS5005", "OBS6005", "OBS7005", "OBS8005", "OBS9005"],
            ObservablesPackageReferenceMappings.ReactivePackageByDiagnosticId.Keys.OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public void R3PackageByReactivePackageId_includes_redis()
    {
        Assert.True(ObservablesPackageReferenceMappings.R3PackageByReactivePackageId.TryGetValue(
            "Observables.Redis.Reactive",
            out var r3PackageId));
        Assert.Equal("Observables.Redis.R3", r3PackageId);
    }

    [Fact]
    public void TryGetDomain_maps_redis_missing_boundary_and_shape_diagnostics()
    {
        Assert.True(ObservablesMemberDiagnosticIds.TryGetDomain("OBS11001", out var missingDomain));
        Assert.Equal(ObservablesMemberDiagnosticIds.InterfaceProxyDomain.Redis, missingDomain);

        Assert.True(ObservablesMemberDiagnosticIds.TryGetDomain("OBS11004", out var shapeDomain));
        Assert.Equal(ObservablesMemberDiagnosticIds.InterfaceProxyDomain.Redis, shapeDomain);
    }

    [Fact]
    public void BoundaryAttributeDefaults_redis_publish_and_subscribe()
    {
        Assert.Equal(
            "[RedisPublish(\"Alerts\")]",
            BoundaryAttributeDefaults.MethodAttribute(
                ObservablesMemberDiagnosticIds.InterfaceProxyDomain.Redis,
                "Alerts"));
        Assert.Equal(
            "[RedisSubscribe(\"Alerts\")]",
            BoundaryAttributeDefaults.PropertyAttribute(
                ObservablesMemberDiagnosticIds.InterfaceProxyDomain.Redis,
                "Alerts"));
        Assert.True(BoundaryAttributeDefaults.RequiresMethod("RedisPublishAttribute"));
        Assert.True(BoundaryAttributeDefaults.RequiresProperty("RedisSubscribeAttribute"));
    }
}
