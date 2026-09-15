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
        var interfaces = model.Interfaces.AsArray();

        ProxyRegistrationEmitter.Emit(
            hintName: "SseProxyRegistration.g.cs",
            registrationClassName: "SseProxyRegistration",
            registerGeneratedFactoryMetadataName: "global::Observables.Sse.SseService.RegisterGeneratedFactory",
            registrations: interfaces.Select(static m =>
                new ProxyRegistrationEmitter.ProxyTypeRegistration(
                    m.InterfaceDisplayName,
                    m.GeneratedNamespace,
                    m.ClassName)).ToArray(),
            addSource);

        EmitEndpointNames(interfaces, addSource);
    }

    /// <summary>
    /// Emits the names carried by <c>[Sse(endpointName)]</c> so that <c>SseService.For&lt;T&gt;()</c> can resolve a
    /// registered endpoint without reading attributes reflectively, which the trim and AOT analyzers reject.
    /// </summary>
    /// <remarks>
    /// Relies on <see cref="ProxyRegistrationEmitter"/> having emitted the <c>ModuleInitializerAttribute</c>
    /// polyfill that netstandard2.0 needs: a named interface is also a registered interface, so that file is
    /// always present alongside this one.
    /// </remarks>
    static void EmitEndpointNames(
        IReadOnlyList<SseInterfaceModel> interfaces,
        Action<string, SourceText> addSource)
    {
        var named = interfaces.Where(static m => m.EndpointName is not null).ToArray();
        if (named.Length == 0)
        {
            return;
        }

        var registrationCalls = string.Join(
            "\n",
            named.Select(static m =>
                $"            global::Observables.Sse.SseService.RegisterProxyName(typeof({m.InterfaceDisplayName}), {FormatLiteral(m.EndpointName!)});"));

        addSource(
            "SseProxyNames.g.cs",
            GeneratedSourceHeader.ToSourceText(
                $$"""
                namespace {{named[0].GeneratedNamespace}}
                {
                    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
                    internal static class SseProxyNames
                    {
                        [global::System.Runtime.CompilerServices.ModuleInitializer]
                        internal static void Initialize()
                        {
                {{registrationCalls}}
                        }
                    }
                }
                """));
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
                    private {{member.ReturnTypeDisplay}}? {{IdentifierHelper.BackingFieldName(member.MemberName)}};
                    public {{member.ReturnTypeDisplay}} {{member.MemberName}} =>
                        {{IdentifierHelper.BackingFieldName(member.MemberName)}} ??= {{BridgeType}}.FromEvent<{{member.ResultTypeDisplay}}>(_connection, {{FormatLiteral(member.EventName)}});

                """),
            trim: new ProxyClassEmitter.TrimWarnings(
                "SSE payload deserialization uses System.Text.Json reflection. Preserve payload type members when trimming.",
                "SSE payload deserialization uses System.Text.Json reflection."));

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
