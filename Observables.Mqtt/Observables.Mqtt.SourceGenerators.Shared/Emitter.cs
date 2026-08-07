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
    public static SourceText EmitInterface(MqttInterfaceModel model)
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
                [global::System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("MQTT payload serialization uses reflection. Preserve payload type members when trimming.")]
                [global::System.Diagnostics.CodeAnalysis.RequiresDynamicCode("MQTT payload serialization uses reflection.")]
            #endif
                internal sealed class {{model.ClassName}} : {{model.InterfaceDisplayName}}
                {
                    private readonly global::MQTTnet.Client.IMqttClient _client;

                    public {{model.ClassName}}(global::MQTTnet.Client.IMqttClient client)
                    {
                        _client = client;
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
