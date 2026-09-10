using Microsoft.CodeAnalysis.CSharp.Syntax;
using Observables.RestAPI;

namespace Observables.Analyzers;

internal static class RestApiSyntaxSlots
{
    internal static RestApiParameterSlot[] From(MethodDeclarationSyntax method)
    {
        var slots = new List<RestApiParameterSlot>(method.ParameterList.Parameters.Count);
        foreach (var parameter in method.ParameterList.Parameters)
        {
            slots.Add(new RestApiParameterSlot(parameter.Identifier.Text, KindOf(parameter)));
        }

        return slots.ToArray();
    }

    static RestApiDeclaredKind KindOf(ParameterSyntax parameter)
    {
        if (parameter.Type?.ToString() is "CancellationToken" or "System.Threading.CancellationToken")
            return RestApiDeclaredKind.Cancellation;

        foreach (var attribute in parameter.AttributeLists.SelectMany(list => list.Attributes))
        {
            var kind = KindFromAttributeName(attribute.Name.ToString());
            if (kind != RestApiDeclaredKind.None)
                return kind;
        }

        return RestApiDeclaredKind.None;
    }

    static RestApiDeclaredKind KindFromAttributeName(string name)
    {
        var simple = name;
        var dot = name.LastIndexOf('.');
        if (dot >= 0)
            simple = name.Substring(dot + 1);
        if (!simple.EndsWith("Attribute", StringComparison.Ordinal))
            simple += "Attribute";

        return simple switch
        {
            "BodyAttribute" => RestApiDeclaredKind.Body,
            "QueryAttribute" => RestApiDeclaredKind.Query,
            "HeaderAttribute" => RestApiDeclaredKind.Header,
            "HeaderCollectionAttribute" => RestApiDeclaredKind.HeaderCollection,
            "AuthorizeAttribute" => RestApiDeclaredKind.Authorize,
            "PropertyAttribute" => RestApiDeclaredKind.Property,
            _ => RestApiDeclaredKind.None,
        };
    }
}
