using System.Globalization;
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
        var members = model.Members.AsArray();
        var reservedNames = new HashSet<string>(
            members.Select(static member => member.MemberName.TrimStart('@')),
            StringComparer.Ordinal)
        {
            model.ClassName,
        };
        var bindings = members.Select(member =>
            (Member: member, FieldName: ReserveMethodFieldName(member, reservedNames))).ToArray();

        return ProxyClassEmitter.Emit(
            model.Nullability,
            model.GeneratedNamespace,
            model.ClassName,
            model.InterfaceDisplayName,
            new ProxyClassEmitter.ClientField(
                "global::Grpc.Core.CallInvoker",
                "_invoker",
                "invoker"),
            bindings,
            static (writer, binding) => EmitMember(writer, binding.Member, binding.FieldName),
            trim: new ProxyClassEmitter.TrimWarnings(
                "gRPC proxy uses reflection for message marshalling and method invocation. Preserve required members when trimming.",
                "gRPC proxy uses reflection for message marshalling and method invocation."),
            emitBeforeMembers: writer =>
            {
                foreach (var binding in bindings)
                {
                    EmitMethodField(writer, binding.Member, model.ServiceName, binding.FieldName);
                }

                writer.WriteLine(string.Empty);
            });
    }

    static string ReserveMethodFieldName(GrpcMemberModel member, HashSet<string> reservedNames)
    {
        var baseName = member.MemberName.TrimStart('@') + "Method";
        var name = baseName;
        var parameterNames = member.ParameterNames.AsArray();
        for (var suffix = 1; parameterNames.Contains(name) || !reservedNames.Add(name); suffix++)
        {
            name = baseName + "_" + suffix.ToString(CultureInfo.InvariantCulture);
        }

        return name;
    }

    static void EmitMethodField(SourceWriter writer, GrpcMemberModel member, string serviceName, string fieldName)
    {
        var methodType = member.BoundaryKind switch
        {
            GrpcBoundaryKind.Unary => "global::Grpc.Core.MethodType.Unary",
            GrpcBoundaryKind.ServerStream => "global::Grpc.Core.MethodType.ServerStreaming",
            GrpcBoundaryKind.ClientStream => "global::Grpc.Core.MethodType.ClientStreaming",
            GrpcBoundaryKind.Duplex => "global::Grpc.Core.MethodType.DuplexStreaming",
            _ => "global::Grpc.Core.MethodType.Unary",
        };

        var requestType = NonNullableIfString(
            member.BoundaryKind is GrpcBoundaryKind.ClientStream or GrpcBoundaryKind.Duplex
                ? member.StreamRequestTypeDisplay!
                : member.RequestTypeDisplay!);
        var responseType = NonNullableIfString(member.ResultTypeDisplay);
        var requestMarshaller = MarshallerExpression(requestType);
        var responseMarshaller = MarshallerExpression(responseType);

        writer.WriteLine(
            $$"""
                    private static readonly global::Grpc.Core.Method<{{requestType}}, {{responseType}}> {{fieldName}} =
                        new({{methodType}}, {{FormatLiteral(serviceName)}}, {{FormatLiteral(member.RpcName)}}, {{requestMarshaller}}, {{responseMarshaller}});

            """);
    }

    static void EmitMember(SourceWriter writer, GrpcMemberModel member, string fieldName)
    {
        var cancellation = member.CancellationTokenParameterName is { } ctName ? $", {ctName}" : ", default";
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
                        {{BridgeType}}.{{bridgeMethod}}(_invoker, {{fieldName}}, {{requestArg}}{{cancellation}});

            """);
    }

    static string NonNullableIfString(string typeDisplay)
    {
        var core = typeDisplay.EndsWith("?", StringComparison.Ordinal)
            ? typeDisplay.Substring(0, typeDisplay.Length - 1)
            : typeDisplay;
        return core is "global::System.String" or "string" ? core : typeDisplay;
    }

    static string MarshallerExpression(string typeDisplay)
    {
        var core = NonNullableIfString(typeDisplay);
        return core is "global::System.String" or "string"
            ? "global::Observables.Grpc.GrpcMarshallers.String"
            : $"global::Observables.Grpc.GrpcMarshallers.ForMessage<{typeDisplay}>()";
    }

    static string FormatLiteral(string value)
    {
        return "\u0022" + value
            .Replace("\\", "\\\\")
            .Replace("\u0022", "\\\u0022")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t") + "\u0022";
    }
}
