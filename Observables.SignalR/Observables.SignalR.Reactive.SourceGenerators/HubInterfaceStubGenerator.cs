using Microsoft.CodeAnalysis;
using Observables.SignalR.Generators;

namespace Observables.SignalR.Reactive.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class HubInterfaceStubGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        SignalRGeneratorPipeline.Register(context);
    }
}
