using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Observables.SignalR.Generators;

internal static class Emitter
{
#if SIGNALR_R3
    const string BridgeType = "global::Observables.SignalR.SignalRObservable";
#else
    const string BridgeType = "global::Observables.SignalR.Reactive.SystemReactiveSignalRAdapter";
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
                $"            global::Observables.SignalR.HubService.RegisterGeneratedFactory(typeof({m.InterfaceDisplayName}), static c => new {m.GeneratedNamespace}.{m.ClassName}(c));"));

        var ns = model.Interfaces[0].GeneratedNamespace;
        var source = $$"""

            #pragma warning disable
            namespace {{ns}}
            {
                [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
                internal static class HubProxyRegistration
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
            #pragma warning restore

            """;

        addSource("HubProxyRegistration.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    public static SourceText EmitInterface(HubInterfaceModel model)
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
            #if NET8_0_OR_GREATER
                [global::System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("SignalR hub proxy uses reflection for method invocation and serialization. Preserve required members when trimming.")]
                [global::System.Diagnostics.CodeAnalysis.RequiresDynamicCode("SignalR hub proxy uses reflection for method invocation and serialization.")]
            #endif
                internal sealed class {{model.ClassName}} : {{model.InterfaceDisplayName}}
                {
                    private readonly global::Microsoft.AspNetCore.SignalR.Client.HubConnection _connection;

                    public {{model.ClassName}}(global::Microsoft.AspNetCore.SignalR.Client.HubConnection connection)
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

    static void EmitMember(SourceWriter writer, HubMemberModel member)
    {
        if (member.IsProperty)
        {
            writer.WriteLine(
                $$"""
                    private {{member.ReturnTypeDisplay}}? _{{member.MemberName}};
                    public {{member.ReturnTypeDisplay}} {{member.MemberName}} =>
                        _{{member.MemberName}} ??= {{BridgeType}}.FromOn<{{member.ResultTypeDisplay}}>(_connection, "{{member.HubMethodName}}");

                """);
            return;
        }

        var argsExpression = member.ParameterNames.Count == 0
            ? "global::System.Array.Empty<object?>()"
            : "new object?[] { " + string.Join(", ", member.ParameterNames.AsArray()) + " }";

        var cancellation = member.HasCancellationToken ? ", cancellationToken" : ", default";

        var bridgeCall = member.BoundaryKind switch
        {
            HubBoundaryKind.Invoke =>
                $"{BridgeType}.FromInvoke<{member.ResultTypeDisplay}>(_connection, \"{member.HubMethodName}\", {argsExpression}{cancellation})",
            HubBoundaryKind.Send =>
                $"{BridgeType}.FromSend(_connection, \"{member.HubMethodName}\", {argsExpression}{cancellation})",
            HubBoundaryKind.Stream =>
                $"{BridgeType}.FromStream<{member.ResultTypeDisplay}>(_connection, \"{member.HubMethodName}\", {argsExpression}{cancellation})",
            _ => throw new InvalidOperationException("Unexpected boundary for method."),
        };

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
