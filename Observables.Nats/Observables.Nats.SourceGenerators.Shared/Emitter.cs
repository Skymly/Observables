using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Observables.Nats.Generators;

internal static class Emitter
{
#if NATS_R3
    const string BridgeType = "global::Observables.Nats.NatsObservable";
#else
    const string BridgeType = "global::Observables.Nats.Reactive.SystemReactiveNatsAdapter";
#endif

    public static void EmitModuleInitializers(
        ContextGenerationModel model,
        Action<string, SourceText> addSource)
    {
        var interfaces = model.Interfaces.AsArray();

        ProxyRegistrationEmitter.Emit(
            hintName: "NatsProxyRegistration.g.cs",
            registrationClassName: "NatsProxyRegistration",
            registerGeneratedFactoryMetadataName: "global::Observables.Nats.NatsService.RegisterGeneratedFactory",
            registrations: interfaces.Select(static m =>
                new ProxyRegistrationEmitter.ProxyTypeRegistration(
                    m.InterfaceDisplayName,
                    m.GeneratedNamespace,
                    m.ClassName)).ToArray(),
            addSource);

        EmitConnectionNames(interfaces, addSource);
    }

    /// <summary>
    /// Emits the names carried by <c>[Nats(connectionName)]</c> so that <c>NatsService.For&lt;T&gt;()</c> can
    /// resolve a registered connection without reading attributes reflectively, which the trim and AOT
    /// analyzers reject.
    /// </summary>
    /// <remarks>
    /// Relies on <see cref="ProxyRegistrationEmitter"/> having emitted the <c>ModuleInitializerAttribute</c>
    /// polyfill that netstandard2.0 needs: a named interface is also a registered interface, so that file is
    /// always present alongside this one.
    /// </remarks>
    static void EmitConnectionNames(
        IReadOnlyList<NatsInterfaceModel> interfaces,
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
                $"            global::Observables.Nats.NatsService.RegisterProxyName(typeof({m.InterfaceDisplayName}), {FormatLiteral(m.ConnectionName!)});"));

        addSource(
            "NatsProxyNames.g.cs",
            GeneratedSourceHeader.ToSourceText(
                $$"""
                namespace {{named[0].GeneratedNamespace}}
                {
                    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
                    internal static class NatsProxyNames
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
    public static SourceText EmitInterface(NatsInterfaceModel model) =>
        ProxyClassEmitter.Emit(
            model.Nullability,
            model.GeneratedNamespace,
            model.ClassName,
            model.InterfaceDisplayName,
            new ProxyClassEmitter.ClientField(
                "global::NATS.Client.Core.INatsConnection",
                "_connection",
                "connection"),
            model.Members.AsArray(),
            EmitMember,
            trim: new ProxyClassEmitter.TrimWarnings(
                "NATS payload serialization uses reflection. Preserve payload type members when trimming.",
                "NATS payload serialization uses reflection."));

    static void EmitMember(SourceWriter writer, NatsMemberModel member)
    {
        if (member.IsProperty)
        {
            writer.WriteLine(
                $$"""
                    private {{member.ReturnTypeDisplay}}? {{BackingFieldName(member.MemberName)}};
                    public {{member.ReturnTypeDisplay}} {{member.MemberName}} =>
                        {{BackingFieldName(member.MemberName)}} ??= {{BridgeType}}.FromSubscribe<{{member.ResultTypeDisplay}}>(_connection, {{FormatLiteral(member.SubjectTemplate)}});

                """);
            return;
        }

        var subjectExpression = BuildSubjectExpression(member);
        var cancellation = member.CancellationTokenParameterName is { } ctName ? $", {ctName}" : ", default";
        var bridgeCall = BuildBridgeCall(member, subjectExpression, cancellation);

        var parameterList = member.ParameterDeclarations.Count == 0
            ? string.Empty
            : string.Join(", ", member.ParameterDeclarations.AsArray());

        writer.WriteLine(
            $$"""
                public {{member.ReturnTypeDisplay}} {{member.MemberName}}({{parameterList}}) =>
                    {{bridgeCall}};

            """);
    }

    static string BuildBridgeCall(NatsMemberModel member, string subjectExpression, string cancellation)
    {
        if (member.BoundaryKind == NatsBoundaryKind.Request)
        {
            return
                $"{BridgeType}.FromRequest<{member.PayloadTypeDisplay}, {member.ResultTypeDisplay}>(_connection, {subjectExpression}, {member.PayloadParameterName}{cancellation})";
        }

        if (member.PayloadParameterName is not null)
        {
            return
                $"{BridgeType}.FromPublish<{member.PayloadTypeDisplay}>(_connection, {subjectExpression}, {member.PayloadParameterName}{cancellation})";
        }

        return $"{BridgeType}.FromPublish(_connection, {subjectExpression}{cancellation})";
    }

    static string BuildSubjectExpression(NatsMemberModel member)
    {
        if (member.SubjectParameterNames.Count == 0)
        {
            return FormatLiteral(member.SubjectTemplate);
        }

        var args = string.Join(
            ", ",
            member.SubjectParameterNames.AsArray().Select(static n => $"({FormatLiteral(n)}, {IdentifierHelper.Escape(n)})"));
        return $"global::Observables.Nats.NatsSubject.Format({FormatLiteral(member.SubjectTemplate)}, {args})";
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
