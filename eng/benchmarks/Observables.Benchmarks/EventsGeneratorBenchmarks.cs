using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Observables.Events.Generators;

namespace Observables.Benchmarks;

/// <summary>
/// Measures the Events R3 source generator under three scenarios:
///
/// <list type="bullet">
///   <item><b>ColdRun</b>: a fresh <see cref="GeneratorDriver"/> each iteration —
///       full parse (symbols → <c>EventsEmissionModel</c>) + emit.</item>
///   <item><b>IncrementalUnchanged</b>: re-run on the same driver + same compilation —
///       measures Roslyn's built-in reference-equality cache.</item>
///   <item><b>IncrementalUnrelatedEdit</b>: re-run on the same driver after adding a
///       new unrelated syntax tree.  The <c>WithTrackingName</c> parse step produces
///       a value-equal <c>EventsEmissionModel</c>, so Roslyn can skip the emit step.
///       However, the parse step itself still re-executes because
///       <c>CompilationProvider</c> is part of the pipeline and the compilation
///       reference changed.  This benchmark documents that limitation: the parse
///       cost (symbol resolution + source generation) dominates and is not cached
///       across compilation changes.</item>
/// </list>
///
/// Future optimisation: extract <c>CompilationProvider</c> from the parse pipeline
/// so that unrelated compilation changes don't invalidate the parse cache.
/// </summary>
[MemoryDiagnoser]
public class EventsGeneratorBenchmarks
{
    /// <summary>Number of classes with classic .NET events that trigger <c>.Events()</c>.</summary>
    [Params(10, 50, 200)]
    public int EventSourceCount;

    CSharpCompilation _compilation = null!;
    CSharpParseOptions _parseOptions = null!;
    GeneratorDriver _warmDriver = null!;
    CSharpCompilation _editedCompilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            BuildSource(EventSourceCount), _parseOptions);

        _compilation = CSharpCompilation.Create(
            "EventsGeneratorBenchmark",
            [syntaxTree],
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Warm driver — ran once so the parse step is cached.
        _warmDriver = CSharpGeneratorDriver.Create(
            generators: [new ObservableEventsGenerator().AsSourceGenerator()],
            parseOptions: _parseOptions);
        _warmDriver = _warmDriver.RunGenerators(_compilation);

        // Edited compilation: add one unrelated syntax tree (no .Events() calls).
        // This changes the Compilation reference but the generator's input
        // (candidates + config) is structurally identical.
        var unrelatedTree = CSharpSyntaxTree.ParseText(
            "namespace Unrelated; public class Dummy { public int Value { get; set; } }",
            _parseOptions,
            path: "/0/Dummy.cs");
        _editedCompilation = _compilation.AddSyntaxTrees(unrelatedTree);
    }

    /// <summary>Full parse + emit on a fresh driver — no incremental cache.</summary>
    [Benchmark(Baseline = true)]
    public GeneratorDriver ColdRun()
    {
        var driver = CSharpGeneratorDriver.Create(
            generators: [new ObservableEventsGenerator().AsSourceGenerator()],
            parseOptions: _parseOptions);
        return driver.RunGenerators(_compilation);
    }

    /// <summary>Re-run on the same driver + same compilation — reference-equality cache.</summary>
    [Benchmark]
    public GeneratorDriver IncrementalUnchanged() => _warmDriver.RunGenerators(_compilation);

    /// <summary>
    /// Re-run on the same driver after adding an unrelated syntax tree.
    /// This is the scenario <c>WithTrackingName</c> + value-comparable model optimises.
    /// </summary>
    [Benchmark]
    public GeneratorDriver IncrementalUnrelatedEdit() => _warmDriver.RunGenerators(_editedCompilation);

    static string BuildSource(int eventSourceCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using R3;");
        sb.AppendLine("using Observables.Events.R3;");
        sb.AppendLine();

        for (var i = 0; i < eventSourceCount; i++)
        {
            sb.AppendLine($$"""
                namespace Demo{{i}};
                public class EventSource{{i}}
                {
                    public event Action? Changed{{i}};
                    public event EventHandler<EventArgs>? Notified{{i}};
                }
                """);
        }

        // One usage call per source — forces the generator to build the interface
        // hierarchy and emit impl + extension for every type.  Separate methods keep
        // Roslyn's overload resolution fast (no mega-parameter-list).
        sb.AppendLine();
        sb.AppendLine("namespace Demo.Usage;");
        sb.AppendLine("public static class Usage");
        sb.AppendLine("{");
        for (var i = 0; i < eventSourceCount; i++)
        {
            sb.AppendLine($"    public static void Run{i}(Demo{i}.EventSource{i} s{i})");
            sb.AppendLine($"    {{");
            sb.AppendLine($"        _ = s{i}.Events().Changed{i};");
            sb.AppendLine($"        _ = s{i}.Events().Notified{i};");
            sb.AppendLine($"    }}");
        }
        sb.AppendLine("}");

        return sb.ToString();
    }

    static MetadataReference[] GetMetadataReferences()
    {
        var trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string
            ?? throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES is unavailable.");

        var references = trusted
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(global::R3.Unit).Assembly.Location));
        return [.. references];
    }
}
