using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Observables.SourceGenerators.Shared;

namespace Observables.Analyzers.Tests;

public sealed class IdentifierHelperTests
{
    [Fact]
    public void IsCancellationToken_is_true_only_for_non_nullable_CancellationToken()
    {
        const string source =
            """
            using System.Threading;
            public class Sample
            {
                public void Go(CancellationToken ct, CancellationToken? maybe, int n) {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken);
        var compilation = CSharpCompilation.Create(
            "IdentifierHelperTests",
            [tree],
            AnalyzerTestHarness.GetPlatformReferencesExcludingObservables(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var method = compilation.GetTypeByMetadataName("Sample")!.GetMembers("Go").OfType<IMethodSymbol>().Single();
        var ct = method.Parameters[0].Type;
        var maybe = method.Parameters[1].Type;
        var n = method.Parameters[2].Type;

        Assert.True(IdentifierHelper.IsCancellationToken(ct));
        Assert.False(IdentifierHelper.IsCancellationToken(maybe));
        Assert.False(IdentifierHelper.IsCancellationToken(n));
        Assert.Null(IdentifierHelper.TryGetTrailingCancellationToken(method));
    }
}
