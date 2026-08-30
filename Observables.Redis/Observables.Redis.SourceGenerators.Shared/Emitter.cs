using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Observables.Redis.Generators;

internal static class Emitter
{
#if REDIS_R3
    const string BridgeType = "global::Observables.Redis.RedisObservable";
#else
    const string BridgeType = "global::Observables.Redis.Reactive.SystemReactiveRedisAdapter";
#endif

    public static void EmitModuleInitializers(
        ContextGenerationModel model,
        Action<string, SourceText> addSource)
    {
        ProxyRegistrationEmitter.Emit(
            hintName: "RedisProxyRegistration.g.cs",
            registrationClassName: "RedisProxyRegistration",
            registerGeneratedFactoryMetadataName: "global::Observables.Redis.RedisService.RegisterGeneratedFactory",
            registrations: model.Interfaces.AsArray().Select(static m =>
                new ProxyRegistrationEmitter.ProxyTypeRegistration(
                    m.InterfaceDisplayName,
                    m.GeneratedNamespace,
                    m.ClassName)).ToArray(),
            addSource);
    }
    public static SourceText EmitInterface(RedisInterfaceModel model) =>
        ProxyClassEmitter.Emit(
            model.Nullability,
            model.GeneratedNamespace,
            model.ClassName,
            model.InterfaceDisplayName,
            new ProxyClassEmitter.ClientField(
                "global::StackExchange.Redis.IConnectionMultiplexer",
                "_multiplexer",
                "multiplexer"),
            model.Members.AsArray(),
            EmitMember,
            trim: new ProxyClassEmitter.TrimWarnings(
                "Redis payload serialization uses reflection. Preserve payload type members when trimming.",
                "Redis payload serialization uses reflection."));

    static void EmitMember(SourceWriter writer, RedisMemberModel member)
    {
        if (member.IsProperty)
        {
            var subscribeMethod = (member.IsPatternSubscribe, member.UseEnvelope) switch
            {
                (false, false) => "FromSubscribe",
                (true, false) => "FromPatternSubscribe",
                (false, true) => "FromSubscribeMessage",
                (true, true) => "FromPatternSubscribeMessage",
            };

            writer.WriteLine(
                $$"""
                    private {{member.ReturnTypeDisplay}}? _{{member.MemberName}};
                    public {{member.ReturnTypeDisplay}} {{member.MemberName}} =>
                        _{{member.MemberName}} ??= {{BridgeType}}.{{subscribeMethod}}<{{member.ResultTypeDisplay}}>(_multiplexer, "{{member.ChannelTemplate}}");

                """);
            return;
        }

        var channelExpression = BuildChannelExpression(member);
        var cancellation = member.HasCancellationToken ? ", cancellationToken" : ", default";
        var bridgeCall = BuildBridgeCall(member, channelExpression, cancellation);

        var parameterList = member.ParameterDeclarations.Count == 0
            ? string.Empty
            : string.Join(", ", member.ParameterDeclarations.AsArray());

        writer.WriteLine(
            $$"""
                public {{member.ReturnTypeDisplay}} {{member.MemberName}}({{parameterList}}) =>
                    {{bridgeCall}};

            """);
    }

    static string BuildBridgeCall(RedisMemberModel member, string channelExpression, string cancellation)
    {
        if (member.PayloadParameterName is not null)
        {
            return
                $"{BridgeType}.FromPublish<{member.PayloadTypeDisplay}>(_multiplexer, {channelExpression}, {member.PayloadParameterName}{cancellation})";
        }

        return $"{BridgeType}.FromPublish(_multiplexer, {channelExpression}{cancellation})";
    }

    static string BuildChannelExpression(RedisMemberModel member)
    {
        if (member.ChannelParameterNames.Count == 0)
        {
            return $"\"{member.ChannelTemplate}\"";
        }

        var args = string.Join(
            ", ",
            member.ChannelParameterNames.AsArray().Select(static n =>
                $"(\"{n}\", {IdentifierHelper.Escape(n)})"));
        return $"global::Observables.Redis.RedisChannelTemplate.Format(\"{member.ChannelTemplate}\", {args})";
    }
}
