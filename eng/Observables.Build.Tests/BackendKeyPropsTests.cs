namespace Observables.Build.Tests;

public sealed class BackendKeyPropsTests
{
    public static IEnumerable<object[]> DomainBackendProps()
    {
        string[] features =
        [
            "Events", "RestAPI", "SignalR", "Mqtt", "WebSocket",
            "Grpc", "Sse", "Nats", "Postgres", "Redis",
        ];
        foreach (var feature in features)
        {
            foreach (var backend in new[] { "R3", "Reactive" })
            {
                yield return [feature, backend];
            }
        }
    }

    [Theory]
    [MemberData(nameof(DomainBackendProps))]
    public void Package_props_declare_the_backend_key_for_OBS0001(string feature, string backend)
    {
        string path = Path.Combine(
            RepoRoot.Find(),
            $"Observables.{feature}",
            $"Observables.{feature}.Package",
            "build",
            $"Observables.{feature}.{backend}.props");
        Assert.True(File.Exists(path), path);

        string xml = File.ReadAllText(path);
        string key = $"Observables{feature}Backend_{backend}";
        Assert.Contains($"<{key}>", xml, StringComparison.Ordinal);
        Assert.Contains($"Include=\"{key}\"", xml, StringComparison.Ordinal);
    }
}
