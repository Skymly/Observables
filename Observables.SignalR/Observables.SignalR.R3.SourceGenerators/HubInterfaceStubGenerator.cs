using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Observables.SourceGenerators.Shared;
using Observables.SignalR.Generators;

namespace Observables.SignalR.R3.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class HubInterfaceStubGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IoProxyGeneratorPipeline.RegisterForAttributeInterfaces(
            context,
            interfaceMarkerMetadataName: "Observables.SignalR.HubAttribute",
            parse: Parser.GenerateHubStubs,
            internalErrorDescriptor: DiagnosticDescriptors.InternalGeneratorError,
            emptyModelFactory: static () => new ContextGenerationModel(ImmutableEquatableArray.Empty<HubInterfaceModel>()),
            getInterfaces: static model => model.Interfaces,
            reportDiagnosticsTrackingName: SignalRGeneratorStepName.ReportDiagnostics,
            buildInterfacesTrackingName: SignalRGeneratorStepName.BuildSignalR,
            emitInterface: static (model, addSource) => addSource(model.FileName, Emitter.EmitInterface(model)),
            emitModuleInitializers: Emitter.EmitModuleInitializers);
    }
}
