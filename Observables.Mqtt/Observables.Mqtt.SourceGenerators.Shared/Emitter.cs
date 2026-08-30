using System.Text;
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
        ProxyRegistrationEmitter.Emit(
            hintName: "MqttProxyRegistration.g.cs",
            registrationClassName: "MqttProxyRegistration",
            registerGeneratedFactoryMetadataName: "global::Observables.Mqtt.MqttService.RegisterGeneratedFactory",
            registrations: model.Interfaces.AsArray().Select(static m =>
                new ProxyRegistrationEmitter.ProxyTypeRegistration(
                    m.InterfaceDisplayName,
                    m.GeneratedNamespace,
                    m.ClassName)).ToArray(),
            addSource);
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
                    private {{member.ReturnTypeDisplay}}? _{{member.MemberName}};
                    public {{member.ReturnTypeDisplay}} {{member.MemberName}} =>
                        _{{member.MemberName}} ??= {{BridgeType}}.FromSubscribe<{{member.ResultTypeDisplay}}>(_client, "{{member.TopicTemplate}}");

                """);
            return;
        }

        var topicExpression = BuildTopicExpression(member);
        var cancellation = member.HasCancellationToken ? ", cancellationToken" : ", default";

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
            return $"\"{member.TopicTemplate}\"";
        }

        var args = string.Join(
            ", ",
            member.TopicParameterNames.AsArray().Select(static n => $"\"{n}\", {IdentifierHelper.Escape(n)}"));
        return $"global::Observables.Mqtt.MqttTopic.Format(\"{member.TopicTemplate}\", {args})";
    }
}
