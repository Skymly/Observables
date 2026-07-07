#if ROSLYN_4
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Observables.SourceGenerators.Shared;

namespace Observables.RestAPI.Generators;

internal static class IncrementalValuesProviderExtensions
{
    /// <summary>
    /// Registers an output node into an <see cref="IncrementalGeneratorInitializationContext"/> to output a diagnostic.
    /// </summary>
    /// <param name="context">The input <see cref="IncrementalGeneratorInitializationContext"/> instance.</param>
    /// <param name="diagnostic">The input <see cref="IncrementalValuesProvider{TValues}"/> sequence of diagnostics.</param>
    public static void ReportDiagnostics(
        this IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<Diagnostic> diagnostic
    )
    {
        context.RegisterSourceOutput(
            diagnostic,
            static (context, diagnostic) => context.ReportDiagnostic(diagnostic)
        );
    }

    /// <summary>
    /// Registers an implementation source output for the provided mappers.
    /// </summary>
    /// <param name="context">The context, on which the output is registered.</param>
    /// <param name="model">The interfaces stubs.</param>
    public static void EmitSource(
        this IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<InterfaceModel> model
    )
    {
        context.RegisterImplementationSourceOutput(
            model,
            static (spc, model) =>
                GeneratorFailSafe.TryEmit(
                    () => spc.AddSource(model.FileName, Emitter.EmitInterface(model)),
                    spc.ReportDiagnostic,
                    DiagnosticDescriptors.InternalGeneratorError));
    }

    public static void EmitSharedCode(
        this IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<ContextGenerationModel> contextModel)
    {
        context.RegisterImplementationSourceOutput(
            contextModel,
            static (spc, model) =>
                GeneratorFailSafe.TryEmit(
                    () => Emitter.EmitSharedCode(model, (name, code) => spc.AddSource(name, code)),
                    spc.ReportDiagnostic,
                    DiagnosticDescriptors.InternalGeneratorError));
    }
}
#endif
