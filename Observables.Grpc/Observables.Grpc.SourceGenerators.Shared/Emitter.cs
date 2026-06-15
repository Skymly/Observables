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
        if (model.Interfaces.Count == 0)
        {
            return;
        }

        var dependencyAttributes = string.Join(
            "\n",
            model.Interfaces.AsArray().Select(static m =>
                $"                    [global::System.Diagnostics.CodeAnalysis.DynamicDependency(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, typeof({m.GeneratedNamespace}.{m.ClassName}))]"));

        var registrations = string.Join(
            "\n",
            model.Interfaces.AsArray().Select(static m =>
                $"            global::Observables.Grpc.GrpcService.RegisterGeneratedFactory(typeof({m.InterfaceDisplayName}), static c => new {m.GeneratedNamespace}.{m.ClassName}(c));"));

        var ns = model.Interfaces[0].GeneratedNamespace;
        var source = $$"""

            #pragma warning disable
            namespace {{ns}}
            {
                [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
                internal static class GrpcProxyRegistration
                {
            #if NET5_0_OR_GREATER
            {{dependencyAttributes}}
                    [System.Runtime.CompilerServices.ModuleInitializer]
                    internal static void Initialize()
                    {
            {{registrations}}
                    }
            #endif
                }
            }
            #pragma warning restore

            """;

        addSource("GrpcProxyRegistration.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    public static SourceText EmitInterface(GrpcInterfaceModel model)
    {
        var writer = new SourceWriter();

        if (model.Nullability != Nullability.None)
        {
            writer.WriteLine(
                "#nullable " + (model.Nullability == Nullability.Enabled ? "enable" : "disable"));
        }

        writer.WriteLine(
            $$"""
            #pragma warning disable
            namespace {{model.GeneratedNamespace}}
            {
                [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
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
        var requestArg = member.BoundaryKind is GrpcBoundaryKind.ClientStream or GrpcBoundaryKind.Duplex
            ? member.ParameterNames.AsArray()[0]
            : member.ParameterNames.AsArray()[0];

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
