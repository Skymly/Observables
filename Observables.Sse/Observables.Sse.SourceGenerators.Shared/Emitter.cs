using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Observables.Sse.Generators;

internal static class Emitter
{
#if SSE_R3
    const string BridgeType = "global::Observables.Sse.SseObservable";
#else
    const string BridgeType = "global::Observables.Sse.Reactive.SystemReactiveSseAdapter";
#endif

    public static void EmitModuleInitializers(
        ContextGenerationModel model,
        Action<string, SourceText> addSource)
    {
        ProxyRegistrationEmitter.Emit(
            hintName: "SseProxyRegistration.g.cs",
            registrationClassName: "SseProxyRegistration",
            registerGeneratedFactoryMetadataName: "global::Observables.Sse.SseService.RegisterGeneratedFactory",
            registrations: model.Interfaces.AsArray().Select(static m =>
                new ProxyRegistrationEmitter.ProxyTypeRegistration(
                    m.InterfaceDisplayName,
                    m.GeneratedNamespace,
                    m.ClassName)).ToArray(),
            addSource);
    }
    public static SourceText EmitInterface(SseInterfaceModel model) =>
        ProxyClassEmitter.Emit(
            model.Nullability,
            model.GeneratedNamespace,
            model.ClassName,
            model.InterfaceDisplayName,
            new ProxyClassEmitter.ClientField(
                "global::Observables.Sse.SseConnection",
                "_connection",
                "connection"),
            model.Members.AsArray(),
            (writer, member) => writer.WriteLine(
                $$"""
                    private {{member.ReturnTypeDisplay}}? _{{member.MemberName}};
                    public {{member.ReturnTypeDisplay}} {{member.MemberName}} =>
                        _{{member.MemberName}} ??= {{BridgeType}}.FromEvent<{{member.ResultTypeDisplay}}>(_connection, "{{member.EventName}}");

                """),
            trim: new ProxyClassEmitter.TrimWarnings(
                "SSE payload deserialization uses System.Text.Json reflection. Preserve payload type members when trimming.",
                "SSE payload deserialization uses System.Text.Json reflection."));
}
