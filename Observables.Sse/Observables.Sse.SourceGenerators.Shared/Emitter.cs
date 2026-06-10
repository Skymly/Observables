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

        var registrations = string.Join(
            "\n",
            model.Interfaces.AsArray().Select(static m =>
                $"            global::Observables.Sse.SseService.RegisterGeneratedFactory(typeof({m.InterfaceDisplayName}), static c => new {m.GeneratedNamespace}.{m.ClassName}(c));"));

        var ns = model.Interfaces[0].GeneratedNamespace;
        var source = $$"""

            #pragma warning disable
            namespace {{ns}}
            {
                [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
                internal static class SseProxyRegistration
                {
            #if NET5_0_OR_GREATER
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

        addSource("SseProxyRegistration.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    public static SourceText EmitInterface(SseInterfaceModel model)
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
