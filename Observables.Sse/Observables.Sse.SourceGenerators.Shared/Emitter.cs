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
        if (model.Interfaces.Count == 0)
        {
            return;
        }

        var dependencyAttributes = string.Join(
            "\n",
            model.Interfaces.AsArray().Select(static m =>
                $"                    [global::System.Diagnostics.CodeAnalysis.DynamicDependency(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors | global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods | global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties, typeof({m.GeneratedNamespace}.{m.ClassName}))]"));

        var registrations = string.Join(
            "\n",
            model.Interfaces.AsArray().Select(static m =>
                $"            global::Observables.Sse.SseService.RegisterGeneratedFactory(typeof({m.InterfaceDisplayName}), static c => new {m.GeneratedNamespace}.{m.ClassName}(c));"));

        var ns = model.Interfaces[0].GeneratedNamespace;
        addSource(
            "SseProxyRegistration.g.cs",
            GeneratedSourceHeader.ToSourceText(
                $$"""
                namespace {{ns}}
                {
                    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
                    internal static class SseProxyRegistration
                    {
                #if NET5_0_OR_GREATER
                {{dependencyAttributes}}
                        [System.Runtime.CompilerServices.ModuleInitializer]
                        [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("ILLink", "IL2026", Justification = "Factory registration only; the proxy is invoked by user code that declares RequiresUnreferencedCode.")]
                        internal static void Initialize()
                        {
                {{registrations}}
                        }
                #endif
                    }
                }
                """));
    }

    public static SourceText EmitInterface(SseInterfaceModel model)
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
                [global::System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("SSE payload deserialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
                [global::System.Diagnostics.CodeAnalysis.RequiresDynamicCode("SSE payload deserialization uses System.Text.Json reflection.")]
            #endif
                internal sealed class {{model.ClassName}} : {{model.InterfaceDisplayName}}
                {
                    private readonly global::Observables.Sse.SseConnection _connection;

                    public {{model.ClassName}}(global::Observables.Sse.SseConnection connection)
                    {
                        _connection = connection;
                    }

            """);

        foreach (var member in model.Members.AsArray())
        {
            writer.WriteLine(
                $$"""
                    private {{member.ReturnTypeDisplay}}? _{{member.MemberName}};
                    public {{member.ReturnTypeDisplay}} {{member.MemberName}} =>
                        _{{member.MemberName}} ??= {{BridgeType}}.FromEvent<{{member.ResultTypeDisplay}}>(_connection, "{{member.EventName}}");

                """);
        }

        writer.WriteLine(
            """
                }
            }
            #pragma warning restore
            """);

        return writer.ToSourceText();
    }
}
