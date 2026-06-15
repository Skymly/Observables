using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Observables.WebSocket.Generators;

internal static class Emitter
{
#if WEBSOCKET_R3
    const string BridgeType = "global::Observables.WebSocket.WebSocketObservable";
#else
    const string BridgeType = "global::Observables.WebSocket.Reactive.SystemReactiveWebSocketAdapter";
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
                $"            global::Observables.WebSocket.WebSocketService.RegisterGeneratedFactory(typeof({m.InterfaceDisplayName}), static c => new {m.GeneratedNamespace}.{m.ClassName}(c));"));

        var ns = model.Interfaces[0].GeneratedNamespace;
        var source = $$"""

            #pragma warning disable
            namespace {{ns}}
            {
                [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
                internal static class WebSocketProxyRegistration
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

        addSource("WebSocketProxyRegistration.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    public static SourceText EmitInterface(WebSocketInterfaceModel model)
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
                    private readonly global::System.Net.WebSockets.ClientWebSocket _socket;

                    public {{model.ClassName}}(global::System.Net.WebSockets.ClientWebSocket socket)
                    {
                        _socket = socket;
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

    static void EmitMember(SourceWriter writer, WebSocketMemberModel member)
    {
        switch (member.BoundaryKind)
        {
            case WebSocketBoundaryKind.Receive:
                writer.WriteLine(
                    $$"""
                        private {{member.ReturnTypeDisplay}}? _{{member.MemberName}};
                        public {{member.ReturnTypeDisplay}} {{member.MemberName}} =>
                            _{{member.MemberName}} ??= {{BridgeType}}.FromReceive<{{member.ResultTypeDisplay}}>(_socket);

                    """);
                break;

            case WebSocketBoundaryKind.Connect:
                {
                    var cancellation = member.HasCancellationToken ? ", cancellationToken" : ", default";
                    var uriParam = member.ParameterNames.Count > 0 ? member.ParameterNames.AsArray()[0] : "uri";
                    var parameterList = member.ParameterDeclarations.Count == 0
                        ? string.Empty
                        : string.Join(", ", member.ParameterDeclarations.AsArray());
                    writer.WriteLine(
                        $$"""
                        public {{member.ReturnTypeDisplay}} {{member.MemberName}}({{parameterList}}) =>
                            {{BridgeType}}.FromConnect(_socket, {{uriParam}}{{cancellation}});

                    """);
                    break;
                }

            case WebSocketBoundaryKind.Close:
                {
                    var cancellation = member.HasCancellationToken ? ", cancellationToken" : ", default";
                    var parameterList = member.ParameterDeclarations.Count == 0
                        ? string.Empty
                        : string.Join(", ", member.ParameterDeclarations.AsArray());
                    writer.WriteLine(
                        $$"""
                        public {{member.ReturnTypeDisplay}} {{member.MemberName}}({{parameterList}}) =>
                            {{BridgeType}}.FromClose(_socket{{cancellation}});

                    """);
                    break;
                }

            default: // Send
                {
                    var cancellation = member.HasCancellationToken ? ", cancellationToken" : ", default";
                    var parameterList = member.ParameterDeclarations.Count == 0
                        ? string.Empty
                        : string.Join(", ", member.ParameterDeclarations.AsArray());

                    if (member.ParameterNames.Count == 0)
                    {
                        // No payload — send empty binary frame
                        writer.WriteLine(
                            $$"""
                            public {{member.ReturnTypeDisplay}} {{member.MemberName}}({{parameterList}}) =>
                                {{BridgeType}}.FromSend(_socket, global::System.Array.Empty<byte>(){{cancellation}});

                        """);
                    }
                    else if (member.ParameterNames.Count == 1)
                    {
                        var paramName = member.ParameterNames.AsArray()[0];
                        var paramDecl = member.ParameterDeclarations.Count > 0
                            ? member.ParameterDeclarations.AsArray()[0]
                            : string.Empty;

                        if (paramDecl.Contains("string") || paramDecl.Contains("String"))
                        {
                            // string → Text frame
                            writer.WriteLine(
                                $$"""
                                public {{member.ReturnTypeDisplay}} {{member.MemberName}}({{parameterList}}) =>
                                    {{BridgeType}}.FromSendText(_socket, {{paramName}}{{cancellation}});

                            """);
                        }
                        else if (paramDecl.Contains("byte[]") || paramDecl.Contains("Byte[]"))
                        {
                            // byte[] → Binary frame
                            writer.WriteLine(
                                $$"""
                                public {{member.ReturnTypeDisplay}} {{member.MemberName}}({{parameterList}}) =>
                                    {{BridgeType}}.FromSend(_socket, {{paramName}}{{cancellation}});

                            """);
                        }
                        else
                        {
                            // Custom type → JSON-serialized Text frame (net8+ only)
                            writer.WriteLine(
                                $$"""
                                public {{member.ReturnTypeDisplay}} {{member.MemberName}}({{parameterList}})
                                {
                            #if NET8_0_OR_GREATER
                                    return {{BridgeType}}.FromSendText(_socket, global::System.Text.Json.JsonSerializer.Serialize({{paramName}}){{cancellation}});
                            #else
                                    throw new global::System.NotSupportedException(
                                        "Sending WebSocket payloads of types other than string or byte[] requires net8.0 or later.");
                            #endif
                                }

                            """);
                        }
                    }
                    else
                    {
                        // Multiple params → JSON anonymous object → Text frame (net8+ only)
                        var paramNames = string.Join(", ", member.ParameterNames.AsArray());
                        writer.WriteLine(
                            $$"""
                            public {{member.ReturnTypeDisplay}} {{member.MemberName}}({{parameterList}})
                            {
                        #if NET8_0_OR_GREATER
                                return {{BridgeType}}.FromSendText(_socket, global::System.Text.Json.JsonSerializer.Serialize(new { {{paramNames}} }){{cancellation}});
                        #else
                                throw new global::System.NotSupportedException(
                                    "Sending WebSocket payloads with multiple parameters requires net8.0 or later.");
                        #endif
                            }

                        """);
                    }

                    break;
                }
        }
    }
}
