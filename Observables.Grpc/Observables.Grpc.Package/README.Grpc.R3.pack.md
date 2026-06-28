# Observables.Grpc.R3

Declarative gRPC client proxies with Roslyn source generators — annotate interfaces with `[GrpcUnary]`/`[GrpcServerStream]` to generate [R3](https://github.com/Cysharp/R3) `Observable<T>` proxies for gRPC/protobuf realtime RPC.

## Install

```xml
<PackageReference Include="Observables.Grpc.R3" Version="0.1.1" />
<PackageReference Include="R3" Version="1.3.0" />
<PackageReference Include="Grpc.Net.Client" Version="2.67.0" />
```

## Usage

```csharp
using Grpc.Net.Client;
using Observables.Grpc;
using R3;

[Grpc("echo.Echo")]
public interface IEchoService
{
    [GrpcUnary("UnaryEcho")]
    Observable<EchoReply> UnaryEcho(EchoRequest request, CancellationToken cancellationToken = default);
}

var channel = GrpcChannel.ForAddress("https://localhost:5001");
var client = GrpcService.For<IEchoService>(channel.CreateCallInvoker());
await client.UnaryEcho(new EchoRequest { Text = "hello" }).FirstAsync();
```
