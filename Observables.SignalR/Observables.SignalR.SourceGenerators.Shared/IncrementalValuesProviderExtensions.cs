#if ROSLYN_4
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Observables.SignalR.Generators;

internal static class IncrementalValuesProviderExtensions
{
    public static void ReportDiagnostics(
        this IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<ImmutableEquatableArray<Diagnostic>> diagnostics)
    {
        context.RegisterSourceOutput(
            diagnostics,
            static (context, diagnostics) =>
            {
                foreach (var diagnostic in diagnostics)
                {
                    context.ReportDiagnostic(diagnostic);
                }
            });
    }

    public static void EmitSource(
        this IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<HubInterfaceModel> model)
    {
        context.RegisterImplementationSourceOutput(
            model,
            static (spc, model) => spc.AddSource(model.FileName, Emitter.EmitInterface(model)));
    }
}
#endif
