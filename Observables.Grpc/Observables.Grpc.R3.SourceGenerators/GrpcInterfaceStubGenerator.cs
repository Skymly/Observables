using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Observables.Grpc.Generators;
using Observables.Roslyn.Shared;
using Observables.SourceGenerators.Shared;

namespace Observables.Grpc.R3.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class GrpcInterfaceStubGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IoProxyGeneratorPipeline.RegisterForAttributeInterfaces(
            context,
            domain: ProxyDomainTable.Grpc,
            parse: Parser.GenerateGrpcStubs,
            internalErrorDescriptor: DiagnosticDescriptors.InternalGeneratorError,
            emptyModelFactory: static () => new ContextGenerationModel(ImmutableEquatableArray.Empty<GrpcInterfaceModel>()),
            getInterfaces: static model => model.Interfaces,
            reportDiagnosticsTrackingName: GrpcGeneratorStepName.ReportDiagnostics,
            buildInterfacesTrackingName: GrpcGeneratorStepName.BuildGrpc,
            emitInterface: static (model, addSource) => addSource(model.FileName, Emitter.EmitInterface(model)),
            emitModuleInitializers: Emitter.EmitModuleInitializers);
    }
}
