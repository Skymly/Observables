using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Observables.Mqtt.Generators;

internal static class Emitter
{
#if MQTT_R3
    const string BridgeType = "global::Observables.Mqtt.MqttObservable";
#else
    const string BridgeType = "global::Observables.Mqtt.Reactive.SystemReactiveMqttAdapter";
#endif

    public static void EmitModuleInitializers(
        ContextGenerationModel model,
        Action<string, SourceText> addSource)
    {
        var interfaces = model.Interfaces.AsArray();

        ProxyRegistrationEmitter.Emit(
            hintName: "MqttProxyRegistration.g.cs",
            registrationClassName: "MqttProxyRegistration",
            registerGeneratedFactoryMetadataName: "global::Observables.Mqtt.MqttService.RegisterGeneratedFactory",
            registrations: interfaces.Select(static m =>
                new ProxyRegistrationEmitter.ProxyTypeRegistration(
                    m.InterfaceDisplayName,
                    m.GeneratedNamespace,
                    m.ClassName)).ToArray(),
            addSource);

        EmitClientNames(interfaces, addSource);
    }

    /// <summary>
    /// Emits the names carried by <c>[Mqtt(clientName)]</c> so that <c>MqttService.For&lt;T&gt;()</c> can resolve a
    /// registered client without reading attributes reflectively, which the trim and AOT analyzers reject.
    /// </summary>
    /// <remarks>
    /// Relies on <see cref="ProxyRegistrationEmitter"/> having emitted the <c>ModuleInitializerAttribute</c>
    /// polyfill that netstandard2.0 needs: a named interface is also a registered interface, so that file is
    /// always present alongside this one.
    /// </remarks>
    static void EmitClientNames(
        IReadOnlyList<MqttInterfaceModel> interfaces,
        Action<string, SourceText> addSource)
    {
        var named = interfaces.Where(static m => m.ClientName is not null).ToArray();
        if (named.Length == 0)
        {
            return;
        }

        var registrationCalls = string.Join(
            "\n",
            named.Select(static m =>
                $"            global::Observables.Mqtt.MqttService.RegisterProxyName(typeof({m.InterfaceDisplayName}), {FormatLiteral(m.ClientName!)});"));

        addSource(
            "MqttProxyNames.g.cs",
            GeneratedSourceHeader.ToSourceText(
                $$"""
                namespace {{named[0].GeneratedNamespace}}
                {
                    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
                    internal static class MqttProxyNames
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
    public static SourceText EmitInterface(MqttInterfaceModel model) =>
        ProxyClassEmitter.Emit(
            model.Nullability,
            model.GeneratedNamespace,
            model.ClassName,
            model.InterfaceDisplayName,
            new ProxyClassEmitter.ClientField(
                "global::MQTTnet.Client.IMqttClient",
                "_client",
                "client"),
            model.Members.AsArray(),
            EmitMember,
            trim: new ProxyClassEmitter.TrimWarnings(
                "MQTT payload serialization uses reflection. Preserve payload type members when trimming.",
                "MQTT payload serialization uses reflection."));

    static void EmitMember(SourceWriter writer, MqttMemberModel member)
    {
        if (member.IsProperty)
        {
            writer.WriteLine(
                $$"""
                    private {{member.ReturnTypeDisplay}}? {{BackingFieldName(member.MemberName)}};
                    public {{member.ReturnTypeDisplay}} {{member.MemberName}} =>
                        {{BackingFieldName(member.MemberName)}} ??= {{BridgeType}}.FromSubscribe<{{member.ResultTypeDisplay}}>(_client, {{FormatLiteral(member.TopicTemplate)}});

                """);
            return;
        }

        var topicExpression = BuildTopicExpression(member);
        var cancellation = member.CancellationTokenParameterName is { } ctName ? $", {ctName}" : ", default";

        var bridgeCall =
            $"{BridgeType}.FromPublish(_client, {topicExpression}{cancellation})";

        var parameterList = member.ParameterDeclarations.Count == 0
            ? string.Empty
            : string.Join(", ", member.ParameterDeclarations.AsArray());

        writer.WriteLine(
            $$"""
                public {{member.ReturnTypeDisplay}} {{member.MemberName}}({{parameterList}}) =>
                    {{bridgeCall}};

            """);
    }

    static string BuildTopicExpression(MqttMemberModel member)
    {
        if (member.TopicParameterNames.Count == 0)
        {
            return FormatLiteral(member.TopicTemplate);
        }

        var args = string.Join(
            ", ",
            member.TopicParameterNames.AsArray().Select(static n => $"({FormatLiteral(n)}, {IdentifierHelper.Escape(n)})"));
        return $"global::Observables.Mqtt.MqttTopic.Format({FormatLiteral(member.TopicTemplate)}, {args})";
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

    static string BackingFieldName(string identifier)
    {
        var unescaped = identifier.Length > 0 && identifier[0] == '@'
            ? identifier.Substring(1)
            : identifier;
        return "_" + unescaped;
    }
}
