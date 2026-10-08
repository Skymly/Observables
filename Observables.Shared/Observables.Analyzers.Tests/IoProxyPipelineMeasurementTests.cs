using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Observables.Roslyn.Shared;
using Observables.SourceGenerators.Shared;

namespace Observables.Analyzers.Tests;

public sealed class IoProxyPipelineMeasurementTests
{
    [Fact]
    public void Parse_invocations_are_counted_separately_from_emit_cache_hits()
    {
        var driver = CreateDriver();
        var compilation = CreateCompilation();

        IoProxyPipelineMeasurement.Reset();
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        Assert.True(IoProxyPipelineMeasurement.ParseInvocations >= 1);

        IoProxyPipelineMeasurement.Reset();
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        var unchanged = driver.GetRunResult().Results[0];
        var parseOnUnchanged = IoProxyPipelineMeasurement.ParseInvocations;
        var emitOnUnchanged = IoProxyPipelineMeasurement.EmitInterfaceInvocations;
        var emitReasonOnUnchanged = GetStepReason(unchanged, "Measure.Build");
        var emitCacheHitOnUnchanged = IsCacheHit(emitReasonOnUnchanged);

        IoProxyPipelineMeasurement.Reset();
        var modifiedCompilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("class Extra {}", cancellationToken: TestContext.Current.CancellationToken));
        driver = driver.RunGenerators(modifiedCompilation, TestContext.Current.CancellationToken);
        var modified = driver.GetRunResult().Results[0];
        var parseOnModified = IoProxyPipelineMeasurement.ParseInvocations;
        var emitOnModified = IoProxyPipelineMeasurement.EmitInterfaceInvocations;
        var emitReasonOnModified = GetStepReason(modified, "Measure.Build");
        var emitCacheHitOnModified = IsCacheHit(emitReasonOnModified);

        Assert.Equal(0, parseOnUnchanged);
        Assert.Equal(0, emitOnUnchanged);
        Assert.True(emitCacheHitOnUnchanged, $"Expected emit cache hit, got {emitReasonOnUnchanged}.");

        Assert.True(parseOnModified >= 1);
        Assert.Equal(0, emitOnModified);
        Assert.True(emitCacheHitOnModified, $"Expected emit cache hit, got {emitReasonOnModified}.");
        Assert.NotEqual(0, parseOnModified);
    }

    static bool IsCacheHit(IncrementalStepRunReason reason) =>
        reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged;

    static IncrementalStepRunReason GetStepReason(GeneratorRunResult result, string stepName)
    {
        if (!result.TrackedSteps.TryGetValue(stepName, out var steps) || steps.Length == 0 || steps[^1].Outputs.Length == 0)
        {
            throw new InvalidOperationException(
                $"Tracked step '{stepName}' has no output. Available: [{string.Join(", ", result.TrackedSteps.Keys)}]");
        }

        return steps[^1].Outputs[^1].Reason;
    }

    static CSharpCompilation CreateCompilation()
    {
        var tree = CSharpSyntaxTree.ParseText("public class App {}", cancellationToken: TestContext.Current.CancellationToken);
        return CSharpCompilation.Create(
            "Measure",
            [tree],
            AnalyzerTestHarness.GetPlatformReferencesExcludingObservables(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    static GeneratorDriver CreateDriver()
    {
        return CSharpGeneratorDriver.Create(
            [new MeasurementGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));
    }

    sealed class MeasurementGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var internalError = new DiagnosticDescriptor(
                "TEST000",
                "internal",
                "{0}",
                "Test",
                DiagnosticSeverity.Error,
                true);
            IoProxyGeneratorPipeline.RegisterForAttributeInterfaces(
                context,
                ProxyDomainTable.Mqtt,
                parse: static (_, _, _) => (new List<Diagnostic>(), "stable"),
                internalErrorDescriptor: internalError,
                emptyModelFactory: static () => "stable",
                getInterfaces: static model => new[] { model },
                reportDiagnosticsTrackingName: "Measure.Diagnostics",
                buildInterfacesTrackingName: "Measure.Build",
                emitInterface: static (model, add) => add("Probe.g.cs", SourceText.From("// " + model)),
                emitModuleInitializers: static (_, add) => add("Init.g.cs", SourceText.From("// init")));
        }
    }
}
