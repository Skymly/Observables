using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Observables.SourceGenerators.Shared;
using Observables.Roslyn.Shared;
using Observables.Sse.Generators;

namespace Observables.Sse.Reactive.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class SseInterfaceStubGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IoProxyGeneratorPipeline.RegisterForAttributeInterfaces(
            context,
            domain: ProxyDomainTable.Sse,
            parse: Parser.GenerateSseStubs,
            internalErrorDescriptor: DiagnosticDescriptors.InternalGeneratorError,
            emptyModelFactory: static () => new ContextGenerationModel(ImmutableEquatableArray.Empty<SseInterfaceModel>()),
            getInterfaces: static model => model.Interfaces,
            reportDiagnosticsTrackingName: SseGeneratorStepName.ReportDiagnostics,
            buildInterfacesTrackingName: SseGeneratorStepName.BuildSse,
            emitInterface: static (model, addSource) => addSource(model.FileName, Emitter.EmitInterface(model)),
            emitModuleInitializers: Emitter.EmitModuleInitializers);
    }
}
