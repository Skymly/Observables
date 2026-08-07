using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Observables.SourceGenerators.Shared;
using Observables.WebSocket.Generators;

namespace Observables.WebSocket.R3.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class WebSocketInterfaceStubGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IoProxyGeneratorPipeline.RegisterForAttributeInterfaces(
            context,
            interfaceMarkerMetadataName: "Observables.WebSocket.WebSocketAttribute",
            parse: Parser.GenerateWebSocketStubs,
            internalErrorDescriptor: DiagnosticDescriptors.InternalGeneratorError,
            emptyModelFactory: static () => new ContextGenerationModel(ImmutableEquatableArray.Empty<WebSocketInterfaceModel>()),
            getInterfaces: static model => model.Interfaces,
            reportDiagnosticsTrackingName: WebSocketGeneratorStepName.ReportDiagnostics,
            buildInterfacesTrackingName: WebSocketGeneratorStepName.BuildWebSocket,
            emitInterface: static (model, addSource) => addSource(model.FileName, Emitter.EmitInterface(model)),
            emitModuleInitializers: Emitter.EmitModuleInitializers);
    }
}
