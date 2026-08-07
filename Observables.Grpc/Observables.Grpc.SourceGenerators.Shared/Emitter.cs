using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Observables.Grpc.Generators;

internal static class Emitter
{
#if GRPC_R3
    const string BridgeType = "global::Observables.Grpc.GrpcObservable";
#else
    const string BridgeType = "global::Observables.Grpc.Reactive.SystemReactiveGrpcAdapter";
#endif

    public static void EmitModuleInitializers(
        ContextGenerationModel model,
        Action<string, SourceText> addSource)
    {
        ProxyRegistrationEmitter.Emit(
            hintName: "GrpcProxyRegistration.g.cs",
            registrationClassName: "GrpcProxyRegistration",
            registerGeneratedFactoryMetadataName: "global::Observables.Grpc.GrpcService.RegisterGeneratedFactory",
            registrations: model.Interfaces.AsArray().Select(static m =>
                new ProxyRegistrationEmitter.ProxyTypeRegistration(
                    m.InterfaceDisplayName,
                    m.GeneratedNamespace,
                    m.ClassName)).ToArray(),
            addSource);
    }
    public static SourceText EmitInterface(GrpcInterfaceModel model)
    {
        var writer = new SourceWriter();
        GeneratedSourceHeader.WritePrefix(writer, model.Nullability);

        writer.WriteLine(
            $$"""
            namespace {{model.GeneratedNamespace}}
            {
                [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
            #if NET8_0_OR_GREATER
                [global::System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("gRPC proxy uses reflection for message marshalling and method invocation. Preserve required members when trimming.")]
                [global::System.Diagnostics.CodeAnalysis.RequiresDynamicCode("gRPC proxy uses reflection for message marshalling and method invocation.")]
            #endif
                internal sealed class {{model.ClassName}} : {{model.InterfaceDisplayName}}
                {
                    private readonly global::Grpc.Core.CallInvoker _invoker;

                    public {{model.ClassName}}(global::Grpc.Core.CallInvoker invoker)
                    {
                        _invoker = invoker;
                    }

            """);

        foreach (var member in model.Members.AsArray())
        {
            EmitMethodField(writer, member, model.ServiceName);
        }

        writer.WriteLine(string.Empty);

        foreach (var member in model.Members.AsArray())
        {
            EmitMember(writer, member);
        }

        writer.WriteLine(
            """
                }
            }
            #pragma warning restore
            """);

        return writer.ToSourceText();
    }

    static void EmitMethodField(SourceWriter writer, GrpcMemberModel member, string serviceName)
    {
        var methodType = member.BoundaryKind switch
        {
            GrpcBoundaryKind.Unary => "global::Grpc.Core.MethodType.Unary",
            GrpcBoundaryKind.ServerStream => "global::Grpc.Core.MethodType.ServerStreaming",
            GrpcBoundaryKind.ClientStream => "global::Grpc.Core.MethodType.ClientStreaming",
            GrpcBoundaryKind.Duplex => "global::Grpc.Core.MethodType.DuplexStreaming",
            _ => "global::Grpc.Core.MethodType.Unary",
        };

        var requestType = member.BoundaryKind is GrpcBoundaryKind.ClientStream or GrpcBoundaryKind.Duplex
            ? member.StreamRequestTypeDisplay!
            : member.RequestTypeDisplay!;
        var responseType = member.ResultTypeDisplay;
        var requestMarshaller = MarshallerExpression(requestType);
        var responseMarshaller = MarshallerExpression(responseType);

        writer.WriteLine(
            $$"""
                    private static readonly global::Grpc.Core.Method<{{requestType}}, {{responseType}}> {{member.MemberName}}Method =
                        new({{methodType}}, "{{serviceName}}", "{{member.RpcName}}", {{requestMarshaller}}, {{responseMarshaller}});

            """);
    }

    static void EmitMember(SourceWriter writer, GrpcMemberModel member)
    {
        var cancellation = member.HasCancellationToken ? ", cancellationToken" : ", default";
        var parameterList = member.ParameterDeclarations.Count == 0
            ? string.Empty
            : string.Join(", ", member.ParameterDeclarations.AsArray());
        var requestArg = member.ParameterNames.AsArray()[0];

        var bridgeMethod = member.BoundaryKind switch
        {
            GrpcBoundaryKind.Unary => "FromUnary",
            GrpcBoundaryKind.ServerStream => "FromServerStreaming",
            GrpcBoundaryKind.ClientStream => "FromClientStreaming",
            GrpcBoundaryKind.Duplex => "FromDuplexStreaming",
            _ => "FromUnary",
        };

        writer.WriteLine(
            $$"""
                    public {{member.ReturnTypeDisplay}} {{member.MemberName}}({{parameterList}}) =>
                        {{BridgeType}}.{{bridgeMethod}}(_invoker, {{member.MemberName}}Method, {{requestArg}}{{cancellation}});

            """);
    }

    static string MarshallerExpression(string typeDisplay) =>
        typeDisplay is "global::System.String" or "string"
            ? "global::Observables.Grpc.GrpcMarshallers.String"
            : $"global::Observables.Grpc.GrpcMarshallers.ForMessage<{typeDisplay}>()";
}
