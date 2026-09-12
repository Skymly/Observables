using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Observables.TestSupport;

namespace Observables.TestSupport.Tests;

public sealed class GeneratorTestRunnerTests
{
    static readonly SnapshotOptions ObsSnapshotOptions = new("OBS");

    [Fact]
    public void ToSnapshot_fails_when_output_has_a_CS_error()
    {
        var output = new GeneratorRunOutput(
            ImmutableArray.Create(new GeneratedSource("Broken.g.cs", "class Broken {}")),
            ImmutableArray.Create(
                CreateDiagnostic("CS1002", DiagnosticSeverity.Error, "; expected"),
                CreateDiagnostic("OBS5004", DiagnosticSeverity.Error, "Subscribe must be a property")));

        var exception = Assert.Throws<InvalidOperationException>(
            () => GeneratorTestRunner.ToSnapshot(output, ObsSnapshotOptions));

        Assert.Contains("CS1002", exception.Message, StringComparison.Ordinal);
        Assert.Contains("; expected", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToSnapshot_keeps_only_OBS_diagnostics_in_the_golden_file()
    {
        var output = new GeneratorRunOutput(
            ImmutableArray.Create(new GeneratedSource("Proxy.g.cs", "class Proxy {}")),
            ImmutableArray.Create(
                CreateDiagnostic("OBS5004", DiagnosticSeverity.Error, "Subscribe must be a property"),
                CreateDiagnostic("CS0414", DiagnosticSeverity.Warning, "field is assigned but never used")));

        string snapshot = GeneratorTestRunner.ToSnapshot(output, ObsSnapshotOptions);

        Assert.Contains("OBS5004: Subscribe must be a property", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("CS0414", snapshot, StringComparison.Ordinal);
        Assert.Contains("--- Proxy.g.cs ---", snapshot, StringComparison.Ordinal);
        Assert.Contains("class Proxy", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void ToSnapshot_fails_when_generated_source_does_not_compile()
    {
        var output = GeneratorTestRunner.Run(
            userSource: "public class User { }",
            buildHarnessDocument: static source => source,
            references: GeneratorTestRunner.GetMetadataReferences(),
            generators: [new EmitsInvalidCSharpGenerator()]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => GeneratorTestRunner.ToSnapshot(output, ObsSnapshotOptions));

        Assert.Contains("Generated compilation produced CS errors:", exception.Message, StringComparison.Ordinal);
        Assert.Matches(@"CS\d+", exception.Message);
    }

    [Fact]
    public void ToSnapshot_ignores_CS_errors_in_user_source()
    {
        var output = GeneratorTestRunner.Run(
            userSource: "public class User { int x = ; }",
            buildHarnessDocument: static source => source,
            references: GeneratorTestRunner.GetMetadataReferences(),
            generators: [new EmitsValidCSharpGenerator()]);

        string snapshot = GeneratorTestRunner.ToSnapshot(output, ObsSnapshotOptions);

        Assert.Contains("class Valid", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void ToSnapshot_succeeds_when_generated_source_compiles()
    {
        var output = GeneratorTestRunner.Run(
            userSource: "public class User { }",
            buildHarnessDocument: static source => source,
            references: GeneratorTestRunner.GetMetadataReferences(),
            generators: [new EmitsValidCSharpGenerator()]);

        string snapshot = GeneratorTestRunner.ToSnapshot(output, ObsSnapshotOptions);

        Assert.Contains("  <none>", snapshot, StringComparison.Ordinal);
        Assert.Contains("class Valid", snapshot, StringComparison.Ordinal);
    }

    static Diagnostic CreateDiagnostic(string id, DiagnosticSeverity severity, string message)
    {
        var descriptor = new DiagnosticDescriptor(
            id,
            title: id,
            messageFormat: message,
            category: "Test",
            defaultSeverity: severity,
            isEnabledByDefault: true);
        return Diagnostic.Create(descriptor, Location.None);
    }

    sealed class EmitsInvalidCSharpGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(static ctx =>
                ctx.AddSource("Broken.g.cs", "class Broken { int x = ; }"));
        }
    }

    sealed class EmitsValidCSharpGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(static ctx =>
                ctx.AddSource("Valid.g.cs", "class Valid { }"));
        }
    }
}
