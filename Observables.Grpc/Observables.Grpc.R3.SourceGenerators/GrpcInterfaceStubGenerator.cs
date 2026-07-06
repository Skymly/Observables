using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Observables.Grpc.Generators;
using Observables.SourceGenerators.Shared;

namespace Observables.Grpc.R3.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class GrpcInterfaceStubGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidateInterfaces = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Observables.Grpc.GrpcAttribute",
            static (node, _) => node is InterfaceDeclarationSyntax,
            static (ctx, _) => (InterfaceDeclarationSyntax)ctx.TargetNode);

        var pipeline = candidateInterfaces
            .Collect()
            .Combine(context.CompilationProvider)
            .Select(static (pair, _) => (pair.Left, pair.Right));

        var parseStep = pipeline.Select(
            static (input, ct) =>
                GeneratorFailSafe.ExecuteParse(
                    () => Parser.GenerateGrpcStubs((CSharpCompilation)input.Right, input.Left, ct),
                    DiagnosticDescriptors.InternalGeneratorError,
                    () => new ContextGenerationModel(ImmutableEquatableArray.Empty<GrpcInterfaceModel>())));

        var diagnostics = parseStep
            .Select(static (x, _) => x.diagnostics.ToImmutableEquatableArray())
            .WithTrackingName(GrpcGeneratorStepName.ReportDiagnostics);
        context.ReportDiagnostics(diagnostics);

        var contextModel = parseStep.Select(static (x, _) => x.model);
        var interfaceModels = contextModel
            .SelectMany(static (x, _) => x.Interfaces)
            .WithTrackingName(GrpcGeneratorStepName.BuildGrpc);
        context.EmitSource(interfaceModels);
        context.EmitModuleInitializers(contextModel);
    }
}
