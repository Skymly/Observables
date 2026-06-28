using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Observables.Mqtt.R3.SourceGenerators;

namespace Observables.Benchmarks;

/// <summary>
/// Measures the Mqtt R3 source generator end-to-end (<see cref="GeneratorDriver.RunGenerators"/>)
/// as the number of UNRELATED (non-<c>[Mqtt]</c>) interfaces in the compilation grows.
///
/// The generator uses <c>ForAttributeWithMetadataName("Observables.Mqtt.MqttAttribute", …)</c>,
/// so unrelated declarations are filtered before any semantic work. Generation time should stay
/// roughly flat as <see cref="IrrelevantInterfaceCount"/> increases — this benchmark is the
/// regression guard for that property.
/// </summary>
[MemoryDiagnoser]
public class MqttGeneratorBenchmarks
{
    /// <summary>Unrelated interfaces (no <c>[Mqtt]</c>) the syntax filter must skip.</summary>
    [Params(0, 250, 1000)]
    public int IrrelevantInterfaceCount;

    /// <summary>Real <c>[Mqtt]</c> interfaces the generator actually has to emit proxies for.</summary>
    const int MqttInterfaceCount = 10;

    GeneratorDriver _driver = null!;
    CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            BuildSource(MqttInterfaceCount, IrrelevantInterfaceCount), parseOptions);

        _compilation = CSharpCompilation.Create(
            "GeneratorBenchmark",
            [syntaxTree],
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        _driver = CSharpGeneratorDriver.Create(
            generators: [new MqttInterfaceStubGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

        // Warm up once outside the measured region to JIT the generator + Roslyn paths.
        _ = _driver.RunGenerators(_compilation);
    }

    [Benchmark]
    public GeneratorDriver RunGenerators() => _driver.RunGenerators(_compilation);

    static string BuildSource(int mqttCount, int irrelevantCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using R3;");
        sb.AppendLine("using Observables.Mqtt;");
        sb.AppendLine();
        sb.AppendLine("public sealed class TemperatureReading { public double Celsius { get; set; } }");
        sb.AppendLine();

        for (var i = 0; i < irrelevantCount; i++)
        {
            sb.AppendLine($"public interface IUnrelated{i} {{ void Method{i}(int a, string b); int Value{i} {{ get; }} }}");
        }

        for (var i = 0; i < mqttCount; i++)
        {
            sb.AppendLine($$"""
                [Mqtt]
                public interface ISensorTopics{{i}}
                {
                    [MqttPublish("commands/{deviceId}/restart")]
                    Observable<Unit> Restart{{i}}(string deviceId);

                    [MqttSubscribe("sensors/+/temperature")]
                    Observable<TemperatureReading> Temperature{{i}} { get; }
                }
                """);
        }

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
        references.Add(MetadataReference.CreateFromFile(typeof(global::Observables.Mqtt.MqttService).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(global::MQTTnet.MqttFactory).Assembly.Location));
        return [.. references];
    }
}
