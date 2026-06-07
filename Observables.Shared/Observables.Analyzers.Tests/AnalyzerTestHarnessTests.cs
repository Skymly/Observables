using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Observables.Analyzers.Tests;

public sealed class AnalyzerTestHarnessTests
{
    [Fact]
    public void Harness_with_only_r3_does_not_include_reactive_bridge_assemblies()
    {
        var compilation = CreateCompilation(
            additionalReferences: [AnalyzerTestHarness.CreateReference<global::R3.Unit>()]);

        var assemblyNames = compilation.References
            .Select(compilation.GetAssemblyOrModuleSymbol)
            .OfType<IAssemblySymbol>()
            .Select(static symbol => symbol.Name)
            .ToArray();

        Assert.Contains("R3", assemblyNames);
        Assert.DoesNotContain(assemblyNames, name => name.StartsWith("Observables.", StringComparison.Ordinal));
    }

    static CSharpCompilation CreateCompilation(IEnumerable<MetadataReference> additionalReferences)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText("namespace Test; public interface IMarker { }");
        var references = AnalyzerTestHarness.GetPlatformReferencesExcludingObservables()
            .Concat(additionalReferences)
            .ToArray();

        return CSharpCompilation.Create(
            "HarnessTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
