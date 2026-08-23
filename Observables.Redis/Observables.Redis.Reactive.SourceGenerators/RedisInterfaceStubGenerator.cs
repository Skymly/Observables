using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Observables.SourceGenerators.Shared;
using Observables.Roslyn.Shared;
using Observables.Redis.Generators;

namespace Observables.Redis.Reactive.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class RedisInterfaceStubGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IoProxyGeneratorPipeline.RegisterForAttributeInterfaces(
            context,
            domain: ProxyDomainTable.Redis,
            parse: Parser.GenerateRedisStubs,
            internalErrorDescriptor: DiagnosticDescriptors.InternalGeneratorError,
            emptyModelFactory: static () => new ContextGenerationModel(ImmutableEquatableArray.Empty<RedisInterfaceModel>()),
            getInterfaces: static model => model.Interfaces,
            reportDiagnosticsTrackingName: RedisGeneratorStepName.ReportDiagnostics,
            buildInterfacesTrackingName: RedisGeneratorStepName.BuildRedis,
            emitInterface: static (model, addSource) => addSource(model.FileName, Emitter.EmitInterface(model)),
            emitModuleInitializers: Emitter.EmitModuleInitializers);
    }
}
