using System.Text.RegularExpressions;

namespace Observables.Build.Tests;

public sealed class FalseGreenCiGateTests
{
    [Fact]
    public void Generator_tests_emit_a_single_net8_trx_name()
    {
        IReadOnlyList<string> tfms = TestProjectTfms.GetFrameworks(
            "Observables.Mqtt/Observables.Mqtt.R3.SourceGenerators.Tests/Observables.Mqtt.R3.SourceGenerators.Tests.csproj");

        Assert.Equal(new[] { "net8.0" }, tfms);
        Assert.Equal(
            "Observables.Mqtt.R3.SourceGenerators.Tests.net8.0.trx",
            TestProjectTfms.TrxFileName(
                "Observables.Mqtt.R3.SourceGenerators.Tests.csproj",
                "net8.0"));
    }

    [Fact]
    public void Runtime_e2e_tests_emit_net8_net9_and_net10_trx_names()
    {
        IReadOnlyList<string> tfms = TestProjectTfms.GetFrameworks(
            "Observables.Mqtt/Observables.Mqtt.Tests/Observables.Mqtt.Tests.csproj");

        Assert.Equal(new[] { "net8.0", "net9.0", "net10.0" }, tfms);
        Assert.Equal(
            "Observables.Mqtt.Tests.net9.0.trx",
            TestProjectTfms.TrxFileName("Observables.Mqtt.Tests.csproj", "net9.0"));
    }

    [Fact]
    public void TrimTests_are_not_a_testhost_trx_project()
    {
        string path = "Observables.Shared/Observables.TrimTests/Observables.TrimTests.csproj";
        Assert.True(TestProjectTfms.IsTrimAnalysisProject(path));
        Assert.Empty(TestProjectTfms.GetFrameworks(path));
    }

    [Fact]
    public void Nuke_unit_test_logger_names_trx_files_by_tfm()
    {
        string program = File.ReadAllText(Path.Combine(RepoRoot.Find(), "build", "Program.cs"));

        Assert.Contains("SetFramework(tfm)", program, StringComparison.Ordinal);
        Assert.Contains("TestProjectTfms.TrxFileName(projectFile, tfm)", program, StringComparison.Ordinal);
        Assert.Contains("AssertTrxCompleteness(testProjects)", program, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "LogFileName=\" + projectFile.NameWithoutExtension + \".trx",
            program,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Nuke_ci_publishes_TrimTests_with_ILLink()
    {
        string program = File.ReadAllText(Path.Combine(RepoRoot.Find(), "build", "Program.cs"));

        Assert.Contains("Target TrimPublish", program, StringComparison.Ordinal);
        Assert.Contains("DotNetPublish", program, StringComparison.Ordinal);
        Assert.Contains("PublishTrimmed", program, StringComparison.Ordinal);
        Assert.Contains(".DependsOn(TrimPublish)", program, StringComparison.Ordinal);
        Assert.Contains(".DependsOn(UnitTest)", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Nuke_smoke_runs_consumers_instead_of_build_only()
    {
        string program = File.ReadAllText(Path.Combine(RepoRoot.Find(), "build", "Program.cs"));
        Assert.Contains("DotNetRun", program, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Events.R3.Consumer", "Events()")]
    [InlineData("Events.Reactive.Consumer", "Events()")]
    [InlineData("Events.R3.RoutedEvents.Consumer", "RoutedEvents()")]
    [InlineData("RestAPI.R3.Consumer", "RestService.For")]
    [InlineData("RestAPI.Reactive.Consumer", "RestService.For")]
    [InlineData("Mqtt.R3.Consumer", "MqttService.For")]
    [InlineData("Mqtt.Reactive.Consumer", "MqttService.For")]
    [InlineData("SignalR.R3.Consumer", "HubService.For")]
    [InlineData("SignalR.Reactive.Consumer", "HubService.For")]
    [InlineData("WebSocket.R3.Consumer", "WebSocketService.For")]
    [InlineData("WebSocket.Reactive.Consumer", "WebSocketService.For")]
    [InlineData("Grpc.R3.Consumer", "GrpcService.For")]
    [InlineData("Grpc.R3.NoTransport.Consumer", "GrpcService.For")]
    [InlineData("Grpc.Reactive.Consumer", "GrpcService.For")]
    [InlineData("Grpc.Reactive.NoTransport.Consumer", "GrpcService.For")]
    [InlineData("Sse.R3.Consumer", "SseService.For")]
    [InlineData("Sse.Reactive.Consumer", "SseService.For")]
    [InlineData("Nats.R3.Consumer", "NatsService.For")]
    [InlineData("Nats.Reactive.Consumer", "NatsService.For")]
    [InlineData("Postgres.R3.Consumer", "PostgresService.For")]
    [InlineData("Postgres.Reactive.Consumer", "PostgresService.For")]
    [InlineData("Redis.R3.Consumer", "RedisService.For")]
    [InlineData("Redis.Reactive.Consumer", "RedisService.For")]
    public void Smoke_consumer_touches_a_generated_factory_or_member(string consumer, string token)
    {
        string program = File.ReadAllText(
            Path.Combine(RepoRoot.Find(), "eng", "nuget-smoke", consumer, "Program.cs"));

        Assert.Contains(token, program, StringComparison.Ordinal);
        Assert.False(
            Regex.IsMatch(
                program,
                @"public static void Main\(\)\s*=>\s*Console\.WriteLine",
                RegexOptions.CultureInvariant));
    }
}
