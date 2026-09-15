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
        var interfaces = model.Interfaces.AsArray();

        ProxyRegistrationEmitter.Emit(
            hintName: "RedisProxyRegistration.g.cs",
            registrationClassName: "RedisProxyRegistration",
            registerGeneratedFactoryMetadataName: "global::Observables.Redis.RedisService.RegisterGeneratedFactory",
            registrations: interfaces.Select(static m =>
                new ProxyRegistrationEmitter.ProxyTypeRegistration(
                    m.InterfaceDisplayName,
                    m.GeneratedNamespace,
                    m.ClassName)).ToArray(),
            addSource);

        EmitConnectionNames(interfaces, addSource);
    }

    /// <summary>
    /// Emits the names carried by <c>[Redis(connectionName)]</c> so that <c>RedisService.For&lt;T&gt;()</c> can
    /// resolve a registered multiplexer without reading attributes reflectively, which the trim and AOT
    /// analyzers reject.
    /// </summary>
    /// <remarks>
    /// Relies on <see cref="ProxyRegistrationEmitter"/> having emitted the <c>ModuleInitializerAttribute</c>
    /// polyfill that netstandard2.0 needs: a named interface is also a registered interface, so that file is
    /// always present alongside this one.
    /// </remarks>
    static void EmitConnectionNames(
        IReadOnlyList<RedisInterfaceModel> interfaces,
        Action<string, SourceText> addSource)
    {
        var named = interfaces.Where(static m => m.ConnectionName is not null).ToArray();
        if (named.Length == 0)
        {
            return;
        }

        var registrationCalls = string.Join(
            "\n",
            named.Select(static m =>
                $"            global::Observables.Redis.RedisService.RegisterProxyName(typeof({m.InterfaceDisplayName}), {FormatLiteral(m.ConnectionName!)});"));

        addSource(
            "RedisProxyNames.g.cs",
            GeneratedSourceHeader.ToSourceText(
                $$"""
                namespace {{named[0].GeneratedNamespace}}
                {
                    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
                    internal static class RedisProxyNames
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
                    private {{member.ReturnTypeDisplay}}? {{IdentifierHelper.BackingFieldName(member.MemberName)}};
                    public {{member.ReturnTypeDisplay}} {{member.MemberName}} =>
                        {{IdentifierHelper.BackingFieldName(member.MemberName)}} ??= {{BridgeType}}.{{subscribeMethod}}<{{member.ResultTypeDisplay}}>(_multiplexer, {{FormatLiteral(member.ChannelTemplate)}});

                """);
            return;
        }

        var channelExpression = BuildChannelExpression(member);
        var cancellation = member.CancellationTokenParameterName is { } ctName ? $", {ctName}" : ", default";
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
            return FormatLiteral(member.ChannelTemplate);
        }

        var args = string.Join(
            ", ",
            member.ChannelParameterNames.AsArray().Select(static n =>
                $"({FormatLiteral(n)}, global::System.Convert.ToString((object?){IdentifierHelper.Escape(n)}, global::System.Globalization.CultureInfo.InvariantCulture))"));
        return $"global::Observables.Redis.RedisChannelTemplate.Format({FormatLiteral(member.ChannelTemplate)}, {args})";
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
