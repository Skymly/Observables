using Microsoft.CodeAnalysis;
using Observables.SignalR.Generators;

namespace Observables.SignalR.R3.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class HubInterfaceStubGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        SignalRGeneratorPipeline.Register(context);
    }
}
