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

    [Fact]
    public void Hub_interface_OBS4004_on_hub_on_method()
    {
        const string userSource =
            """
            [Hub]
            public interface IChatHub
            {
                [HubOn("ReceiveMessage")]
                Observable<string> ReceiveMessage();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS4004", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Hub_interface_OBS4005_on_iobservable_with_r3_generator()
    {
        const string userSource =
            """
            [Hub]
            public interface IChatHub
            {
                [HubOn("ReceiveMessage")]
                IObservable<string> ReceiveMessage { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS4005", snapshot, StringComparison.Ordinal);
    }
}
