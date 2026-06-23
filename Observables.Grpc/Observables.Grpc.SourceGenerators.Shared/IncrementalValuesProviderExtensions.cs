#if ROSLYN_4
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Observables.Grpc.Generators;

internal static class IncrementalValuesProviderExtensions
{
    public static void EmitSource(
        this IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<GrpcInterfaceModel> model)
    {
        context.RegisterImplementationSourceOutput(
            model,
            static (spc, model) => spc.AddSource(model.FileName, Emitter.EmitInterface(model)));
    }
}
#endif
