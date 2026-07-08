namespace Observables.SourceGenerators.Shared;

internal static class DiagnosticHelpLink
{
    public const string DocumentationBaseUri =
        "https://skymly.github.io/Observables.Docs/diagnostics.html";

    public static string For(string diagnosticId) =>
        $"{DocumentationBaseUri}#{diagnosticId.ToLowerInvariant()}";
}
