using Microsoft.CodeAnalysis;
using NatsR3 = Observables.Nats.R3.SourceGenerators.NatsInterfaceStubGenerator;
using WebSocketR3 = Observables.WebSocket.R3.SourceGenerators.WebSocketInterfaceStubGenerator;
using WebSocketReactive = Observables.WebSocket.Reactive.SourceGenerators.WebSocketInterfaceStubGenerator;

namespace Observables.TestSupport.Tests;

public sealed class RepresentativeProxyGeneratorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Named_websocket_json_snapshot_compiles_with_and_without_net8_symbols(bool reactive)
    {
        var observable = reactive ? "System.IObservable<System.Reactive.Unit>" : "R3.Observable<R3.Unit>";
        var input = $$"""
            using Observables.WebSocket;

            [WebSocket]
            public interface ISender
            {
                [WebSocketSend("value")]
                {{observable}} Send(Payload value);
            }

            public sealed record Payload(int Value);
            """;
        IIncrementalGenerator generator = reactive ? new WebSocketReactive() : new WebSocketR3();
        var legacy = Run(input, generator, []);
        var modern = Run(input, generator, GeneratorTestRunner.Net8PreprocessorSymbols);

        var options = SnapshotOptionsFactory.ForDomain("OBS6");
        var snapshot = GeneratorTestRunner.ToSnapshot(modern, options);
        Assert.Contains("JsonSerializer.Serialize", snapshot, StringComparison.Ordinal);
        Assert.Contains("SenderGeneratedProxy", snapshot, StringComparison.Ordinal);
        Assert.Equal(GeneratorTestRunner.ToSnapshot(legacy, options), snapshot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Generators_skip_a_proxy_with_an_abstract_generic_member_but_keep_default_helpers(bool websocket)
    {
        var contract = websocket ? "WebSocket" : "Nats";
        var boundary = websocket ? "[WebSocketSend(\"value\")]" : "[NatsPublish(\"value\")]";
        var input = $$"""
            using Observables.{{contract}};
            using R3;

            [{{contract}}]
            public interface IInvalid
            {
                {{boundary}} Observable<Unit> Generic<T>(T payload);
                {{boundary}} Observable<Unit> NonGeneric(string payload);
            }

            [{{contract}}]
            public interface IValid
            {
                T Identity<T>(T value) => value;
                {{boundary}} Observable<Unit> NonGeneric(string payload);
            }
            """;
        IIncrementalGenerator generator = websocket ? new WebSocketR3() : new NatsR3();
        var output = Run(input, generator, GeneratorTestRunner.Net8PreprocessorSymbols);
        var generated = GeneratorTestRunner.GetGeneratedSource(output);
        Assert.Contains("ValidGeneratedProxy", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidGeneratedProxy", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Identity", generated, StringComparison.Ordinal);
    }

    static GeneratorRunOutput Run(
        string input,
        IIncrementalGenerator generator,
        IReadOnlyList<string> symbols) =>
        GeneratorTestRunner.Run(
            userSource: input,
            buildHarnessDocument: static source => source,
            references: GeneratorTestRunner.GetMetadataReferences(),
            generators: [generator],
            preprocessorSymbols: symbols);
}
