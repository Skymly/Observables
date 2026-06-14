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
        if (model.Interfaces.Count == 0)
        {
            return;
        }

        var registrations = string.Join(
            "\n",
            model.Interfaces.AsArray().Select(static m =>
                $"            global::Observables.Nats.NatsService.RegisterGeneratedFactory(typeof({m.InterfaceDisplayName}), static c => new {m.GeneratedNamespace}.{m.ClassName}(c));"));

        var ns = model.Interfaces[0].GeneratedNamespace;
        var source = $$"""

            #pragma warning disable
            namespace {{ns}}
            {
                [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
                internal static class NatsProxyRegistration
                {
            #if NET5_0_OR_GREATER
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

        addSource("NatsProxyRegistration.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    public static SourceText EmitInterface(NatsInterfaceModel model)
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
                    private readonly global::NATS.Client.Core.INatsConnection _connection;

                    public {{model.ClassName}}(global::NATS.Client.Core.INatsConnection connection)
                    {
                        _connection = connection;
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
            member.SubjectParameterNames.AsArray().Select(static n => $"\"{n}\", {n}"));
        return $"global::Observables.Nats.NatsSubject.Format(\"{member.SubjectTemplate}\", {args})";
    }
}
