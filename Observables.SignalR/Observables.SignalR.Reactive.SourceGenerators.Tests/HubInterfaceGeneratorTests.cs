using VerifyXunit;

namespace Observables.SignalR.Reactive.SourceGenerators.Tests;

public sealed class HubInterfaceGeneratorTests
{
    [Fact]
    public Task Hub_interface_generates_proxy_and_registration()
    {
        const string userSource =
            """
            public sealed class ChatMessage
            {
                public string Text { get; set; } = "";
            }

            [Hub]
            public interface IChatHub
            {
                [HubInvoke]
                IObservable<int> GetUserCount();

                [HubOn("ReceiveMessage")]
                IObservable<ChatMessage> ReceiveMessage { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void Hub_interface_uses_system_reactive_bridge()
    {
        const string userSource =
            """
            [Hub]
            public interface IPingHub
            {
                [HubInvoke]
                IObservable<int> Ping();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("OBS4002", snapshot, StringComparison.Ordinal);
        Assert.Contains("SystemReactiveSignalRAdapter", snapshot, StringComparison.Ordinal);
        Assert.Contains("RegisterGeneratedFactory", snapshot, StringComparison.Ordinal);
    }
}
