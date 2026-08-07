using Microsoft.CodeAnalysis.Text;

namespace Observables.SourceGenerators.Shared;

/// <summary>
/// Emits ModuleInitializer registration for single-client
/// <c>*Service.RegisterGeneratedFactory(Type, Func&lt;TClient, T&gt;)</c> proxies.
/// RestAPI (two-arg factory) is out of scope.
/// </summary>
internal static class ProxyRegistrationEmitter
{
    internal readonly struct ProxyTypeRegistration
    {
        internal ProxyTypeRegistration(string interfaceDisplayName, string generatedNamespace, string className)
        {
            InterfaceDisplayName = interfaceDisplayName;
            GeneratedNamespace = generatedNamespace;
            ClassName = className;
        }

        internal string InterfaceDisplayName { get; }
        internal string GeneratedNamespace { get; }
        internal string ClassName { get; }
    }

    /// <param name="hintName">Source hint, e.g. <c>RedisProxyRegistration.g.cs</c>.</param>
    /// <param name="registrationClassName">Generated static class name, e.g. <c>RedisProxyRegistration</c>.</param>
    /// <param name="registerGeneratedFactoryMetadataName">
    /// Fully-qualified call target without trailing call, e.g.
    /// <c>global::Observables.Redis.RedisService.RegisterGeneratedFactory</c>.
    /// </param>
    /// <param name="registrations">Interfaces to register; empty → no source emitted.</param>
    /// <param name="addSource">Destination for the generated file.</param>
    internal static void Emit(
        string hintName,
        string registrationClassName,
        string registerGeneratedFactoryMetadataName,
        IReadOnlyList<ProxyTypeRegistration> registrations,
        Action<string, SourceText> addSource)
    {
        if (registrations.Count == 0)
        {
            return;
        }

        var dependencyAttributes = string.Join(
            "\n",
            registrations.Select(static m =>
                $"                    [global::System.Diagnostics.CodeAnalysis.DynamicDependency(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors | global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods | global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties, typeof({m.GeneratedNamespace}.{m.ClassName}))]"));

        var registrationCalls = string.Join(
            "\n",
            registrations.Select(m =>
                $"            {registerGeneratedFactoryMetadataName}(typeof({m.InterfaceDisplayName}), static c => new {m.GeneratedNamespace}.{m.ClassName}(c));"));

        // Preserve prior domain behavior: file namespace = first interface's generated namespace.
        var ns = registrations[0].GeneratedNamespace;
        addSource(
            hintName,
            GeneratedSourceHeader.ToSourceText(
                $$"""
                namespace {{ns}}
                {
                    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
                    internal static class {{registrationClassName}}
                    {
                #if NET5_0_OR_GREATER
                {{dependencyAttributes}}
                        [System.Runtime.CompilerServices.ModuleInitializer]
                        [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("ILLink", "IL2026", Justification = "Factory registration only; the proxy is invoked by user code that declares RequiresUnreferencedCode.")]
                        internal static void Initialize()
                        {
                {{registrationCalls}}
                        }
                #endif
                    }
                }
                """));
    }

    /// <summary>
    /// Builds the registration source text without adding it (for tests / callers that own <see cref="SourceText"/>).
    /// Returns <see langword="null"/> when <paramref name="registrations"/> is empty.
    /// </summary>
    internal static string? BuildSource(
        string registrationClassName,
        string registerGeneratedFactoryMetadataName,
        IReadOnlyList<ProxyTypeRegistration> registrations)
    {
        if (registrations.Count == 0)
        {
            return null;
        }

        SourceText? captured = null;
        Emit(
            hintName: "unused.g.cs",
            registrationClassName,
            registerGeneratedFactoryMetadataName,
            registrations,
            (_, text) => captured = text);

        return captured?.ToString();
    }
}
