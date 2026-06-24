using VerifyXunit;

namespace Observables.WebSocket.Reactive.SourceGenerators.Tests;

public sealed class WebSocketInterfaceGeneratorTests
{
    [Fact]
    public Task WebSocket_interface_generates_proxy_and_registration()
    {
        const string userSource =
            """
            [WebSocket]
            public interface IMyHub
            {
                [WebSocketConnect]
                IObservable<Unit> Connect(Uri uri, CancellationToken cancellationToken = default);

                [WebSocketClose]
                IObservable<Unit> Close(CancellationToken cancellationToken = default);

                [WebSocketSend("ping")]
                IObservable<Unit> Ping(CancellationToken cancellationToken = default);

                [WebSocketReceive("message")]
                IObservable<string> Messages { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void WebSocket_interface_string_send_generates_FromSendText()
    {
        // string parameter must produce a Text frame (FromSendText), not Binary (FromSend).
        const string userSource =
            """
            [WebSocket]
            public interface IChatHub
            {
                [WebSocketSend]
                IObservable<Unit> SendMessage(string message);

                [WebSocketReceive]
                IObservable<string> Incoming { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("OBS6", snapshot, StringComparison.Ordinal);
        Assert.Contains("ChatHubGeneratedProxy", snapshot, StringComparison.Ordinal);
        // string → FromSendText (Text frame)
        Assert.Contains("FromSendText", snapshot, StringComparison.Ordinal);
        Assert.Contains("FromReceive", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Interface_without_WebSocket_attribute_produces_no_output()
    {
        // An interface without [WebSocket] is simply ignored by the generator; no diagnostics, no generated source.
        const string userSource =
            """
            public interface IPlain
            {
                string Foo { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("OBS6", snapshot, StringComparison.Ordinal);
        Assert.Empty(output.GeneratedSources);
    }

    [Fact]
    public void WebSocket_interface_OBS6001_on_unannotated_member()
    {
        // [WebSocket] interface but one method has no boundary attribute → OBS6001
        const string userSource =
            """
            [WebSocket]
            public interface IBadHub
            {
                IObservable<string> NoAttribute();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS6001", snapshot, StringComparison.Ordinal);
    }
}
