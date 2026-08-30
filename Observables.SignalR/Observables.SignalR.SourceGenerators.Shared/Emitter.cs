using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Observables.SignalR.Generators;

internal static class Emitter
{
#if SIGNALR_R3
    const string BridgeType = "global::Observables.SignalR.SignalRObservable";
#else
    const string BridgeType = "global::Observables.SignalR.Reactive.SystemReactiveSignalRAdapter";
#endif

    public static void EmitModuleInitializers(
        ContextGenerationModel model,
        Action<string, SourceText> addSource)
    {
        ProxyRegistrationEmitter.Emit(
            hintName: "HubProxyRegistration.g.cs",
            registrationClassName: "HubProxyRegistration",
            registerGeneratedFactoryMetadataName: "global::Observables.SignalR.HubService.RegisterGeneratedFactory",
            registrations: model.Interfaces.AsArray().Select(static m =>
                new ProxyRegistrationEmitter.ProxyTypeRegistration(
                    m.InterfaceDisplayName,
                    m.GeneratedNamespace,
                    m.ClassName)).ToArray(),
            addSource);
    }
    public static SourceText EmitInterface(HubInterfaceModel model) =>
        ProxyClassEmitter.Emit(
            model.Nullability,
            model.GeneratedNamespace,
            model.ClassName,
            model.InterfaceDisplayName,
            new ProxyClassEmitter.ClientField(
                "global::Microsoft.AspNetCore.SignalR.Client.HubConnection",
                "_connection",
                "connection"),
            model.Members.AsArray(),
            EmitMember,
            trim: new ProxyClassEmitter.TrimWarnings(
                "SignalR hub proxy uses reflection for method invocation and serialization. Preserve required members when trimming.",
                "SignalR hub proxy uses reflection for method invocation and serialization."));

    static void EmitMember(SourceWriter writer, HubMemberModel member)
    {
        if (member.IsProperty)
        {
            writer.WriteLine(
                $$"""
                    private {{member.ReturnTypeDisplay}}? _{{member.MemberName}};
                    public {{member.ReturnTypeDisplay}} {{member.MemberName}} =>
                        _{{member.MemberName}} ??= {{BridgeType}}.FromOn<{{member.ResultTypeDisplay}}>(_connection, "{{member.HubMethodName}}");

                """);
            return;
        }

        var argsExpression = member.ParameterNames.Count == 0
            ? "global::System.Array.Empty<object?>()"
            : "new object?[] { " + string.Join(", ", member.ParameterNames.AsArray()) + " }";

        var cancellation = member.HasCancellationToken ? ", cancellationToken" : ", default";

        var bridgeCall = member.BoundaryKind switch
        {
            HubBoundaryKind.Invoke =>
                $"{BridgeType}.FromInvoke<{member.ResultTypeDisplay}>(_connection, \"{member.HubMethodName}\", {argsExpression}{cancellation})",
            HubBoundaryKind.Send =>
                $"{BridgeType}.FromSend(_connection, \"{member.HubMethodName}\", {argsExpression}{cancellation})",
            HubBoundaryKind.Stream =>
                $"{BridgeType}.FromStream<{member.ResultTypeDisplay}>(_connection, \"{member.HubMethodName}\", {argsExpression}{cancellation})",
            _ => throw new InvalidOperationException("Unexpected boundary for method."),
        };

        var parameterList = member.ParameterDeclarations.Count == 0
            ? string.Empty
            : string.Join(", ", member.ParameterDeclarations.AsArray());

        writer.WriteLine(
            $$"""
                public {{member.ReturnTypeDisplay}} {{member.MemberName}}({{parameterList}}) =>
                    {{bridgeCall}};

            """);
    }
}
