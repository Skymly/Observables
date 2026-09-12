using Microsoft.CodeAnalysis.CSharp.Syntax;
using Observables.RestAPI;

namespace Observables.Analyzers;

internal static class RestApiPathSuggestions
{
    internal static string SuggestPath(MethodDeclarationSyntax method) =>
        RestApiPathTemplate.Suggest(method.Identifier.Text, RestApiSyntaxSlots.From(method));
}
