using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Observables.SignalR.Reactive.SourceGenerators;

namespace Observables.SignalR.Reactive.SourceGenerators.Tests;

internal static class GeneratorTestHarness
{
    internal static string BuildHarnessDocument(string userSource) =>
        $$"""
        #nullable enable
        using System;
        using System.Threading;
        using System.Reactive;
        using Observables.SignalR;
        using Microsoft.AspNetCore.SignalR.Client;

        {{userSource}}
        """;

    internal static GeneratorRunOutput Run(string userSource)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(BuildHarnessDocument(userSource), parseOptions);

        var references = GetMetadataReferences();
        var compilation = CSharpCompilation.Create(
            "GeneratorTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new HubInterfaceStubGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation outputCompilation,
            out ImmutableArray<Diagnostic> driverDiagnostics);

        var runResult = driver.GetRunResult();
        foreach (var gr in runResult.Results)
        {
            if (gr.Exception is not null)
            {
                throw gr.Exception;
            }
        }

        var generatedSources = runResult.Results
            .SelectMany(static r => r.GeneratedSources)
            .OrderBy(static s => s.HintName, StringComparer.Ordinal)
            .Select(static s => new GeneratedSource(s.HintName, s.SourceText.ToString()))
            .ToImmutableArray();

        var allDiagnostics = runResult.Diagnostics
            .AddRange(driverDiagnostics)
            .AddRange(outputCompilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error));

        return new GeneratorRunOutput(generatedSources, allDiagnostics);
    }

    internal static string ToSnapshot(GeneratorRunOutput output)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Diagnostics:");
        var diagnostics = output.Diagnostics
            .Where(static d => d.Id.StartsWith("OBS4", StringComparison.Ordinal))
            .GroupBy(static d => (d.Id, d.GetMessage()))
            .Select(static g => g.First())
            .OrderBy(static d => d.Id, StringComparer.Ordinal);

        foreach (var diagnostic in diagnostics)
        {
            sb.AppendLine($"  {diagnostic.Id}: {diagnostic.GetMessage()}");
        }

        if (!diagnostics.Any())
        {
            sb.AppendLine("  <none>");
        }

        sb.AppendLine();
        sb.AppendLine("Generated Sources:");
        foreach (var source in output.GeneratedSources)
        {
            sb.AppendLine($"--- {source.HintName} ---");
            sb.AppendLine(source.Source.TrimEnd());
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    static MetadataReference[] GetMetadataReferences()
    {
        var trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string
            ?? throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES is unavailable.");

        var platform = trusted
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToList<MetadataReference>();

        AddReference(platform, typeof(global::System.Reactive.Unit).Assembly.Location);
        AddReference(platform, typeof(global::Observables.SignalR.HubService).Assembly.Location);
        AddReference(platform, typeof(global::Observables.SignalR.Reactive.SystemReactiveSignalRAdapter).Assembly.Location);
        AddReference(platform, typeof(global::Microsoft.AspNetCore.SignalR.Client.HubConnection).Assembly.Location);
        return platform.ToArray();
    }

    static void AddReference(List<MetadataReference> references, string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Required test reference not found: {path}");
        }

        references.Add(MetadataReference.CreateFromFile(path));
    }
}

internal sealed record GeneratorRunOutput(
    ImmutableArray<GeneratedSource> GeneratedSources,
    ImmutableArray<Diagnostic> Diagnostics);

internal sealed record GeneratedSource(string HintName, string Source);
