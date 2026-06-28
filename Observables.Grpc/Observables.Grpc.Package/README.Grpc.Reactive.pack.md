# Observables.Grpc.Reactive

Declarative gRPC client proxies with Roslyn source generators — annotate interfaces with `[GrpcUnary]`/`[GrpcServerStream]` to generate [System.Reactive](https://github.com/dotnet/reactive) `IObservable<T>` proxies for gRPC/protobuf realtime RPC.

## Install

```xml
<PackageReference Include="Observables.Grpc.Reactive" Version="0.1.1" />
<PackageReference Include="System.Reactive" Version="6.0.1" />
<PackageReference Include="Grpc.Net.Client" Version="2.67.0" />
```

## Usage

```csharp
using Grpc.Net.Client;
using Observables.Grpc;
using Observables.Grpc.Reactive;

[Grpc("echo.Echo")]
public interface IEchoService
{
    [GrpcUnary("UnaryEcho")]
    IObservable<EchoReply> UnaryEcho(EchoRequest request, CancellationToken cancellationToken = default);
}

var channel = GrpcChannel.ForAddress("https://localhost:5001");
var client = GrpcService.For<IEchoService>(channel.CreateCallInvoker());
await client.UnaryEcho(new EchoRequest { Text = "hello" }).FirstAsync();
```
