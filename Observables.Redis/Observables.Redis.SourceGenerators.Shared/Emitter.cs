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
                $"            global::Observables.Redis.RedisService.RegisterGeneratedFactory(typeof({m.InterfaceDisplayName}), static c => new {m.GeneratedNamespace}.{m.ClassName}(c));"));

        var ns = model.Interfaces[0].GeneratedNamespace;
        addSource(
            "RedisProxyRegistration.g.cs",
            GeneratedSourceHeader.ToSourceText(
                $$"""
                namespace {{ns}}
                {
                    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
                    internal static class RedisProxyRegistration
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

    public static SourceText EmitInterface(RedisInterfaceModel model)
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
                [global::System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Redis payload serialization uses reflection. Preserve payload type members when trimming.")]
                [global::System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Redis payload serialization uses reflection.")]
            #endif
                internal sealed class {{model.ClassName}} : {{model.InterfaceDisplayName}}
                {
                    private readonly global::StackExchange.Redis.IConnectionMultiplexer _multiplexer;

                    public {{model.ClassName}}(global::StackExchange.Redis.IConnectionMultiplexer multiplexer)
                    {
                        _multiplexer = multiplexer;
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
