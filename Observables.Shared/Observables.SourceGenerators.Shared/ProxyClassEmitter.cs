using Microsoft.CodeAnalysis.Text;

namespace Observables.SourceGenerators.Shared;

/// <summary>
/// Emits the sealed generated proxy class shell for single-client IO Features.
/// Feature emitters keep <c>EmitMember</c> / bridge type as adapters.
/// RestAPI (HTTP request construction, two-arg factory) is out of scope.
/// </summary>
internal static class ProxyClassEmitter
{
    internal readonly struct ClientField
    {
        internal ClientField(string typeMetadataName, string fieldName, string parameterName)
        {
            TypeMetadataName = typeMetadataName;
            FieldName = fieldName;
            ParameterName = parameterName;
        }

        internal string TypeMetadataName { get; }
        internal string FieldName { get; }
        internal string ParameterName { get; }
    }

    internal readonly struct TrimWarnings
    {
        internal TrimWarnings(string unreferencedCode, string dynamicCode)
        {
            UnreferencedCode = unreferencedCode;
            DynamicCode = dynamicCode;
        }

        internal string UnreferencedCode { get; }
        internal string DynamicCode { get; }
    }

    internal static SourceText Emit<TMember>(
        Nullability nullability,
        string generatedNamespace,
        string className,
        string interfaceDisplayName,
        ClientField client,
        IReadOnlyList<TMember> members,
        Action<SourceWriter, TMember> emitMember,
        TrimWarnings? trim = null,
        Action<SourceWriter>? emitBeforeMembers = null)
    {
        var writer = new SourceWriter();
        GeneratedSourceHeader.WritePrefix(writer, nullability);

        writer.WriteLine(
            $$"""
            namespace {{generatedNamespace}}
            {
                [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
            """);

        if (trim is { } warnings)
        {
            writer.WriteLine(
                $$"""
            #if NET8_0_OR_GREATER
                [global::System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("{{warnings.UnreferencedCode}}")]
                [global::System.Diagnostics.CodeAnalysis.RequiresDynamicCode("{{warnings.DynamicCode}}")]
            #endif
            """);
        }

        writer.WriteLine(
            $$"""
                internal sealed class {{className}} : {{interfaceDisplayName}}
                {
                    private readonly {{client.TypeMetadataName}} {{client.FieldName}};

                    public {{className}}({{client.TypeMetadataName}} {{client.ParameterName}})
                    {
                        {{client.FieldName}} = {{client.ParameterName}};
                    }

            """);

        emitBeforeMembers?.Invoke(writer);

        foreach (var member in members)
        {
            emitMember(writer, member);
        }

        writer.WriteLine(
            """
                }
            }
            #pragma warning restore
            """);

        return writer.ToSourceText();
    }
}
