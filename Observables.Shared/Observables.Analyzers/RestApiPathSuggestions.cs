using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Observables.Analyzers;

internal static class RestApiPathSuggestions
{
    internal static string SuggestPath(MethodDeclarationSyntax method)
    {
        var parameters = GetNonCancellationParameterNames(method);
        if (parameters.Count == 0)
        {
            return "/" + method.Identifier.Text.ToLowerInvariant();
        }

        return "/" + string.Join("/", parameters.Select(p => "{" + p + "}"));
    }

    internal static IReadOnlyList<string> GetNonCancellationParameterNames(MethodDeclarationSyntax method) =>
        method.ParameterList.Parameters
            .Where(p => p.Type?.ToString() is not ("CancellationToken" or "System.Threading.CancellationToken"))
            .Select(p => p.Identifier.Text)
            .ToArray();
}
