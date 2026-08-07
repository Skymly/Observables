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
    public static SourceText EmitInterface(HubInterfaceModel model)
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
                [global::System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("SignalR hub proxy uses reflection for method invocation and serialization. Preserve required members when trimming.")]
                [global::System.Diagnostics.CodeAnalysis.RequiresDynamicCode("SignalR hub proxy uses reflection for method invocation and serialization.")]
            #endif
                internal sealed class {{model.ClassName}} : {{model.InterfaceDisplayName}}
                {
                    private readonly global::Microsoft.AspNetCore.SignalR.Client.HubConnection _connection;

                    public {{model.ClassName}}(global::Microsoft.AspNetCore.SignalR.Client.HubConnection connection)
                    {
                        _connection = connection;
                    }

            """);

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
