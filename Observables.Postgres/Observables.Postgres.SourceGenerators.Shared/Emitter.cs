using Microsoft.CodeAnalysis.Text;

namespace Observables.Postgres.Generators;

internal static class Emitter
{
#if POSTGRES_R3
    const string BridgeType = "global::Observables.Postgres.PostgresObservable";
#else
    const string BridgeType = "global::Observables.Postgres.Reactive.SystemReactivePostgresAdapter";
#endif

    public static void EmitModuleInitializers(
        ContextGenerationModel model,
        Action<string, SourceText> addSource)
    {
        ProxyRegistrationEmitter.Emit(
            hintName: "PostgresProxyRegistration.g.cs",
            registrationClassName: "PostgresProxyRegistration",
            registerGeneratedFactoryMetadataName: "global::Observables.Postgres.PostgresService.RegisterGeneratedFactory",
            registrations: model.Interfaces.AsArray().Select(static m =>
                new ProxyRegistrationEmitter.ProxyTypeRegistration(
                    m.InterfaceDisplayName,
                    m.GeneratedNamespace,
                    m.ClassName)).ToArray(),
            addSource);
    }
    public static SourceText EmitInterface(PostgresInterfaceModel model)
    {
        var writer = new SourceWriter();
        GeneratedSourceHeader.WritePrefix(writer, model.Nullability);

        writer.WriteLine(
            $$"""
            namespace {{model.GeneratedNamespace}}
            {
                [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
                internal sealed class {{model.ClassName}} : {{model.InterfaceDisplayName}}
                {
                    private readonly global::Npgsql.NpgsqlConnection _connection;

                    public {{model.ClassName}}(global::Npgsql.NpgsqlConnection connection)
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

    static void EmitMember(SourceWriter writer, PostgresMemberModel member)
    {
        if (member.IsProperty)
        {
            var listenCall = IsStringType(member.ResultTypeDisplay)
                ? $"{BridgeType}.FromListen(_connection, \"{member.ChannelName}\")"
                : $"{BridgeType}.FromListen<{member.ResultTypeDisplay}>(_connection, \"{member.ChannelName}\")";

            writer.WriteLine(
                $$"""
                    private {{member.ReturnTypeDisplay}}? _{{member.MemberName}};
                    public {{member.ReturnTypeDisplay}} {{member.MemberName}} =>
                        _{{member.MemberName}} ??= {{listenCall}};

                """);
            return;
        }

        var cancellation = member.HasCancellationToken ? "cancellationToken" : "default";
        string bridgeCall;
        if (member.PayloadParameterName is null)
        {
            bridgeCall = $"{BridgeType}.FromNotify(_connection, \"{member.ChannelName}\", {cancellation})";
        }
        else if (IsStringType(member.PayloadTypeDisplay))
        {
            bridgeCall =
                $"{BridgeType}.FromNotify(_connection, \"{member.ChannelName}\", {member.PayloadParameterName}, {cancellation})";
        }
        else
        {
            bridgeCall =
                $"{BridgeType}.FromNotify<{member.PayloadTypeDisplay}>(_connection, \"{member.ChannelName}\", {member.PayloadParameterName}, {cancellation})";
        }

        var parameterList = member.ParameterDeclarations.Count == 0
            ? string.Empty
            : string.Join(", ", member.ParameterDeclarations.AsArray());

        writer.WriteLine(
            $$"""
                public {{member.ReturnTypeDisplay}} {{member.MemberName}}({{parameterList}}) =>
                    {{bridgeCall}};

            """);
    }

    static bool IsStringType(string? typeDisplay) =>
        typeDisplay is "string" or "string?" or "global::System.String" or "global::System.String?";
}
