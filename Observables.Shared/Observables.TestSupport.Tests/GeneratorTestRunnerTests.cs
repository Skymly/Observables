using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
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
    public void GetGeneratedSource_fails_on_generated_CS0102_even_if_the_caller_only_reads_source_text()
    {
        var output = GeneratorTestRunner.Run(
            userSource: "public class User { }",
            buildHarnessDocument: static source => source,
            references: GeneratorTestRunner.GetMetadataReferences(),
            generators: [new EmitsDuplicateMemberGenerator()]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => GeneratorTestRunner.GetGeneratedSource(output));

        Assert.Contains("CS0102", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetGeneratedSource_fails_on_generated_CS1503_even_if_the_caller_only_reads_source_text()
    {
        var output = GeneratorTestRunner.Run(
            userSource: "public class User { }",
            buildHarnessDocument: static source => source,
            references: GeneratorTestRunner.GetMetadataReferences(),
            generators: [new EmitsArgumentMismatchGenerator()]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => GeneratorTestRunner.GetGeneratedSource(output));

        Assert.Contains("CS1503", exception.Message, StringComparison.Ordinal);
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

    [Fact]
    public void Default_parse_options_define_net8_symbols()
    {
        (CSharpCompilation compilation, _) = GeneratorTestRunner.CreateHarnessCompilation(
            userSource: "class C { }",
            buildHarnessDocument: static source => source,
            references: GeneratorTestRunner.GetMetadataReferences());

        var options = (CSharpParseOptions)compilation.SyntaxTrees.Single().Options;
        Assert.Contains("NET8_0_OR_GREATER", options.PreprocessorSymbolNames);
        Assert.Contains("NET5_0_OR_GREATER", options.PreprocessorSymbolNames);
    }

    [Fact]
    public void Polyfill_parse_options_omit_tfm_symbols()
    {
        (CSharpCompilation compilation, _) = GeneratorTestRunner.CreateHarnessCompilation(
            userSource: "class C { }",
            buildHarnessDocument: static source => source,
            references: GeneratorTestRunner.GetMetadataReferences(),
            preprocessorSymbols: []);

        var options = (CSharpParseOptions)compilation.SyntaxTrees.Single().Options;
        Assert.DoesNotContain("NET8_0_OR_GREATER", options.PreprocessorSymbolNames);
        Assert.DoesNotContain("NET5_0_OR_GREATER", options.PreprocessorSymbolNames);
    }

    [Fact]
    public void Net8_symbols_skip_the_polyfill_compile_error_branch()
    {
        var output = GeneratorTestRunner.Run(
            userSource: "public class User { }",
            buildHarnessDocument: static source => source,
            references: GeneratorTestRunner.GetMetadataReferences(),
            generators: [new EmitsTfmGatedSyntaxGenerator()]);

        GeneratorTestRunner.AssertCompiled(output);
    }

    [Fact]
    public void Omitting_tfm_symbols_compiles_the_polyfill_error_branch()
    {
        var output = GeneratorTestRunner.Run(
            userSource: "public class User { }",
            buildHarnessDocument: static source => source,
            references: GeneratorTestRunner.GetMetadataReferences(),
            generators: [new EmitsTfmGatedSyntaxGenerator()],
            preprocessorSymbols: []);

        var exception = Assert.Throws<InvalidOperationException>(
            () => GeneratorTestRunner.AssertCompiled(output));

        Assert.Contains("Generated compilation produced CS errors:", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RequireDiagnostic_returns_id_severity_and_source_location()
    {
        var output = GeneratorTestRunner.Run(
            userSource: "public class User { }",
            buildHarnessDocument: static source => source,
            references: GeneratorTestRunner.GetMetadataReferences(),
            generators: [new ReportsObsGenerator()],
            includeResultDiagnostics: true);

        Diagnostic diagnostic = GeneratorTestRunner.RequireDiagnostic(
            output,
            "OBS0001",
            DiagnosticSeverity.Error);

        Assert.Equal("OBS0001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.True(diagnostic.Location.IsInSource);
        Assert.Equal(0, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
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

    sealed class EmitsDuplicateMemberGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(static ctx =>
                ctx.AddSource("Dup.g.cs", "class Dup { public int X; public int X; }"));
        }
    }

    sealed class EmitsArgumentMismatchGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(static ctx =>
                ctx.AddSource(
                    "Mismatch.g.cs",
                    "class Mismatch { static void F(string s) {} void M() { F(1); } }"));
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

    sealed class EmitsTfmGatedSyntaxGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(static ctx =>
                ctx.AddSource(
                    "Tfm.g.cs",
                    """
                    class Tfm
                    {
                    #if !NET8_0_OR_GREATER
                        int x = ;
                    #endif
                    }
                    """));
        }
    }

    sealed class ReportsObsGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(static ctx =>
                ctx.AddSource("Empty.g.cs", "class Empty { }"));
            context.RegisterSourceOutput(
                context.CompilationProvider,
                static (spc, compilation) =>
                {
                    var tree = compilation.SyntaxTrees.First();
                    var location = Location.Create(tree, new TextSpan(0, 1));
                    var descriptor = new DiagnosticDescriptor(
                        "OBS0001",
                        "test",
                        "conflict",
                        "Test",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true);
                    spc.ReportDiagnostic(Diagnostic.Create(descriptor, location));
                });
        }
    }
}
