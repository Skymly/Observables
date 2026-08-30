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
        ProxyRegistrationEmitter.Emit(
            hintName: "NatsProxyRegistration.g.cs",
            registrationClassName: "NatsProxyRegistration",
            registerGeneratedFactoryMetadataName: "global::Observables.Nats.NatsService.RegisterGeneratedFactory",
            registrations: model.Interfaces.AsArray().Select(static m =>
                new ProxyRegistrationEmitter.ProxyTypeRegistration(
                    m.InterfaceDisplayName,
                    m.GeneratedNamespace,
                    m.ClassName)).ToArray(),
            addSource);
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
                    private {{member.ReturnTypeDisplay}}? _{{member.MemberName}};
                    public {{member.ReturnTypeDisplay}} {{member.MemberName}} =>
                        _{{member.MemberName}} ??= {{BridgeType}}.FromSubscribe<{{member.ResultTypeDisplay}}>(_connection, "{{member.SubjectTemplate}}");

                """);
            return;
        }

        var subjectExpression = BuildSubjectExpression(member);
        var cancellation = member.HasCancellationToken ? ", cancellationToken" : ", default";
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
            return $"\"{member.SubjectTemplate}\"";
        }

        var args = string.Join(
            ", ",
            member.SubjectParameterNames.AsArray().Select(static n => $"\"{n}\", {IdentifierHelper.Escape(n)}"));
        return $"global::Observables.Nats.NatsSubject.Format(\"{member.SubjectTemplate}\", {args})";
    }
}
