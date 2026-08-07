using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Observables.SourceGenerators.Shared;
using Observables.Mqtt.Generators;

namespace Observables.Mqtt.R3.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class MqttInterfaceStubGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IoProxyGeneratorPipeline.RegisterForAttributeInterfaces(
            context,
            interfaceMarkerMetadataName: "Observables.Mqtt.MqttAttribute",
            parse: Parser.GenerateMqttStubs,
            internalErrorDescriptor: DiagnosticDescriptors.InternalGeneratorError,
            emptyModelFactory: static () => new ContextGenerationModel(ImmutableEquatableArray.Empty<MqttInterfaceModel>()),
            getInterfaces: static model => model.Interfaces,
            reportDiagnosticsTrackingName: MqttGeneratorStepName.ReportDiagnostics,
            buildInterfacesTrackingName: MqttGeneratorStepName.BuildMqtt,
            emitInterface: static (model, addSource) => addSource(model.FileName, Emitter.EmitInterface(model)),
            emitModuleInitializers: Emitter.EmitModuleInitializers);
    }
}
