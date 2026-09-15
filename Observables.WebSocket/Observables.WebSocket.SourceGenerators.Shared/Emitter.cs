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
        ProxyRegistrationEmitter.Emit(
            hintName: "WebSocketProxyRegistration.g.cs",
            registrationClassName: "WebSocketProxyRegistration",
            registerGeneratedFactoryMetadataName: "global::Observables.WebSocket.WebSocketService.RegisterGeneratedFactory",
            registrations: model.Interfaces.AsArray().Select(static m =>
                new ProxyRegistrationEmitter.ProxyTypeRegistration(
                    m.InterfaceDisplayName,
                    m.GeneratedNamespace,
                    m.ClassName)).ToArray(),
            addSource);
    }
    public static SourceText EmitInterface(WebSocketInterfaceModel model) =>
        ProxyClassEmitter.Emit(
            model.Nullability,
            model.GeneratedNamespace,
            model.ClassName,
            model.InterfaceDisplayName,
            new ProxyClassEmitter.ClientField(
                "global::System.Net.WebSockets.ClientWebSocket",
                "_socket",
                "socket"),
            model.Members.AsArray(),
            EmitMember,
            trim: new ProxyClassEmitter.TrimWarnings(
                "WebSocket payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.",
                "WebSocket payload serialization uses System.Text.Json reflection."));

    static void EmitMember(SourceWriter writer, WebSocketMemberModel member)
    {
        switch (member.BoundaryKind)
        {
            case WebSocketBoundaryKind.Receive:
                // A named receive reads the same JSON envelope a named send writes, so it filters on type
                // instead of taking every frame. Unnamed receives keep the raw stream.
                var receive = member.MessageName is { } receiveName
                    ? $"{BridgeType}.FromReceiveNamed<{member.ResultTypeDisplay}>(_socket, {FormatLiteral(receiveName)})"
                    : $"{BridgeType}.FromReceive<{member.ResultTypeDisplay}>(_socket)";
                writer.WriteLine(
                    $$"""
                    private {{member.ReturnTypeDisplay}}? {{IdentifierHelper.BackingFieldName(member.MemberName)}};
                    public {{member.ReturnTypeDisplay}} {{member.MemberName}} =>
                        {{IdentifierHelper.BackingFieldName(member.MemberName)}} ??= {{receive}};

                    """);
                break;

            case WebSocketBoundaryKind.Connect:
                {
                    var cancellation = member.CancellationTokenParameterName is { } ctName ? $", {ctName}" : ", default";
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
                    var cancellation = member.CancellationTokenParameterName is { } ctName ? $", {ctName}" : ", default";
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
                    var cancellation = member.CancellationTokenParameterName is { } ctName ? $", {ctName}" : ", default";
                    var parameterList = member.ParameterDeclarations.Count == 0
                        ? string.Empty
                        : string.Join(", ", member.ParameterDeclarations.AsArray());

                    if (member.MessageName is { } messageName)
                    {
                        EmitNamedSend(writer, member, messageName, parameterList, cancellation);
                    }
                    else if (member.ParameterNames.Count == 0)
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

                        if (member.SendPayloadKind == WebSocketSendPayloadKind.Text)
                        {
                            // string → Text frame
                            writer.WriteLine(
                                $$"""
                                public {{member.ReturnTypeDisplay}} {{member.MemberName}}({{parameterList}}) =>
                                    {{BridgeType}}.FromSendText(_socket, {{paramName}}{{cancellation}});

                            """);
                        }
                        else if (member.SendPayloadKind == WebSocketSendPayloadKind.Binary)
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

    /// <summary>
    /// Emits a send that declares a message name. The name only exists on the wire, so the payload is wrapped
    /// in a <c>{"type":…,"payload":…}</c> envelope; a send with no arguments omits <c>payload</c>.
    /// </summary>
    static void EmitNamedSend(
        SourceWriter writer,
        WebSocketMemberModel member,
        string messageName,
        string parameterList,
        string cancellation)
    {
        var envelope = member.ParameterNames.Count switch
        {
            0 => $"new {{ type = {FormatLiteral(messageName)} }}",
            1 => $"new {{ type = {FormatLiteral(messageName)}, payload = {member.ParameterNames.AsArray()[0]} }}",
            _ => $"new {{ type = {FormatLiteral(messageName)}, payload = new {{ {string.Join(", ", member.ParameterNames.AsArray())} }} }}",
        };

        writer.WriteLine(
            $$"""
            public {{member.ReturnTypeDisplay}} {{member.MemberName}}({{parameterList}})
            {
        #if NET8_0_OR_GREATER
                return {{BridgeType}}.FromSendText(_socket, global::System.Text.Json.JsonSerializer.Serialize({{envelope}}){{cancellation}});
        #else
                throw new global::System.NotSupportedException(
                    "Sending a WebSocket message that declares a message name requires net8.0 or later.");
        #endif
            }

        """);
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
