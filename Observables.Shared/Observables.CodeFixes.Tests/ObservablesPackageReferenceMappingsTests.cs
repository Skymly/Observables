namespace Observables.CodeFixes.Tests;

public sealed class ObservablesPackageReferenceMappingsTests
{
    [Fact]
    public void RuntimePackageByDiagnosticId_covers_core_not_referenced_diagnostics()
    {
        Assert.Equal(
            ["OBS3002", "OBS4002", "OBS5002", "OBS6002", "OBS7002", "OBS8002", "OBS9002"],
            ObservablesPackageReferenceMappings.RuntimePackageByDiagnosticId.Keys.OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public void ReactivePackageByDiagnosticId_covers_reactive_package_diagnostics()
    {
        Assert.Equal(
            ["OBS3005", "OBS4005", "OBS5005", "OBS6005", "OBS7005", "OBS8005", "OBS9005"],
            ObservablesPackageReferenceMappings.ReactivePackageByDiagnosticId.Keys.OrderBy(id => id, StringComparer.Ordinal));
    }
}
