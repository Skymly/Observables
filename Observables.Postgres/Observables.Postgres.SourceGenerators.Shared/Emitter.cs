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
                $"            global::Observables.Postgres.PostgresService.RegisterGeneratedFactory(typeof({m.InterfaceDisplayName}), static c => new {m.GeneratedNamespace}.{m.ClassName}(c));"));

        var ns = model.Interfaces[0].GeneratedNamespace;
        addSource(
            "PostgresProxyRegistration.g.cs",
            GeneratedSourceHeader.ToSourceText(
                $$"""
                namespace {{ns}}
                {
                    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
                    internal static class PostgresProxyRegistration
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
            writer.WriteLine(
                $$"""
                    private {{member.ReturnTypeDisplay}}? _{{member.MemberName}};
                    public {{member.ReturnTypeDisplay}} {{member.MemberName}} =>
                        _{{member.MemberName}} ??= {{BridgeType}}.FromListen(_connection, "{{member.ChannelName}}");

                """);
            return;
        }

        var cancellation = member.HasCancellationToken ? "cancellationToken" : "default";
        var bridgeCall = member.PayloadParameterName is not null
            ? $"{BridgeType}.FromNotify(_connection, \"{member.ChannelName}\", {member.PayloadParameterName}, {cancellation})"
            : $"{BridgeType}.FromNotify(_connection, \"{member.ChannelName}\", {cancellation})";

        var parameterList = member.ParameterDeclarations.Count == 0
            ? string.Empty
            : string.Join(", ", member.ParameterDeclarations.AsArray());

        writer.WriteLine(
            $$"""
                public {{member.ReturnTypeDisplay}} {{member.MemberName}}({{parameterList}}) =>
                    {{bridgeCall}};

            """);
    }
}
