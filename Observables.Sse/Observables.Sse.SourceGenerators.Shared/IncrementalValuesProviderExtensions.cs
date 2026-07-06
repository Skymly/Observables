#if ROSLYN_4
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Observables.SourceGenerators.Shared;

namespace Observables.Sse.Generators;

internal static class IncrementalValuesProviderExtensions
{
    public static void EmitSource(
        this IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<SseInterfaceModel> model)
    {
        context.RegisterImplementationSourceOutput(
            model,
            static (spc, model) =>
                GeneratorFailSafe.TryEmit(
                    () => spc.AddSource(model.FileName, Emitter.EmitInterface(model)),
                    spc.ReportDiagnostic,
                    DiagnosticDescriptors.InternalGeneratorError));
    }

    public static void EmitModuleInitializers(
        this IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<ContextGenerationModel> contextModel)
    {
        context.RegisterImplementationSourceOutput(
            contextModel,
            static (spc, model) =>
                GeneratorFailSafe.TryEmit(
                    () => Emitter.EmitModuleInitializers(model, (name, code) => spc.AddSource(name, code)),
                    spc.ReportDiagnostic,
                    DiagnosticDescriptors.InternalGeneratorError));
    }
}
#endif
