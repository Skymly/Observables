namespace Observables.SignalR.R3.SourceGenerators.Tests;

public sealed class HubInterfaceGeneratorTests
{
    [Fact]
    public void Hub_interface_generates_proxy_and_registration()
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
                Observable<int> GetUserCount();

                [HubOn("ReceiveMessage")]
                Observable<ChatMessage> ReceiveMessage { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("OBS4002", snapshot, StringComparison.Ordinal);
        Assert.Contains("ChatHubGeneratedProxy", snapshot, StringComparison.Ordinal);
        Assert.Contains("RegisterGeneratedFactory", snapshot, StringComparison.Ordinal);
        Assert.Contains("FromInvoke", snapshot, StringComparison.Ordinal);
        Assert.Contains("FromOn", snapshot, StringComparison.Ordinal);
    }
}
