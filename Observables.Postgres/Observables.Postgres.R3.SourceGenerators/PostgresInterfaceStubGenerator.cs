using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Observables.SourceGenerators.Shared;
using Observables.Postgres.Generators;

namespace Observables.Postgres.R3.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class PostgresInterfaceStubGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IoProxyGeneratorPipeline.RegisterForAttributeInterfaces(
            context,
            interfaceMarkerMetadataName: "Observables.Postgres.PostgresAttribute",
            parse: Parser.GeneratePostgresStubs,
            internalErrorDescriptor: DiagnosticDescriptors.InternalGeneratorError,
            emptyModelFactory: static () => new ContextGenerationModel(ImmutableEquatableArray.Empty<PostgresInterfaceModel>()),
            getInterfaces: static model => model.Interfaces,
            reportDiagnosticsTrackingName: PostgresGeneratorStepName.ReportDiagnostics,
            buildInterfacesTrackingName: PostgresGeneratorStepName.BuildPostgres,
            emitInterface: static (model, addSource) => addSource(model.FileName, Emitter.EmitInterface(model)),
            emitModuleInitializers: Emitter.EmitModuleInitializers);
    }
}
