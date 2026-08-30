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
    public static SourceText EmitInterface(GrpcInterfaceModel model) =>
        ProxyClassEmitter.Emit(
            model.Nullability,
            model.GeneratedNamespace,
            model.ClassName,
            model.InterfaceDisplayName,
            new ProxyClassEmitter.ClientField(
                "global::Grpc.Core.CallInvoker",
                "_invoker",
                "invoker"),
            model.Members.AsArray(),
            EmitMember,
            trim: new ProxyClassEmitter.TrimWarnings(
                "gRPC proxy uses reflection for message marshalling and method invocation. Preserve required members when trimming.",
                "gRPC proxy uses reflection for message marshalling and method invocation."),
            emitBeforeMembers: writer =>
            {
                foreach (var member in model.Members.AsArray())
                {
                    EmitMethodField(writer, member, model.ServiceName);
                }

                writer.WriteLine(string.Empty);
            });

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
