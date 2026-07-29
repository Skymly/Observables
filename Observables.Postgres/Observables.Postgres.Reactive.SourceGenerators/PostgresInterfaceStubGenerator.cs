using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Observables.Postgres.Generators;
using Observables.SourceGenerators.Shared;

namespace Observables.Postgres.Reactive.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class PostgresInterfaceStubGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidateInterfaces = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Observables.Postgres.PostgresAttribute",
            static (node, _) => node is InterfaceDeclarationSyntax,
            static (ctx, _) => (InterfaceDeclarationSyntax)ctx.TargetNode);

        var pipeline = candidateInterfaces
            .Collect()
            .Combine(context.CompilationProvider)
            .Select(static (pair, _) => (pair.Left, pair.Right));

        var parseStep = pipeline.Select(
            static (input, ct) =>
                GeneratorFailSafe.ExecuteParse(
                    () => Parser.GeneratePostgresStubs((CSharpCompilation)input.Right, input.Left, ct),
                    DiagnosticDescriptors.InternalGeneratorError,
                    () => new ContextGenerationModel(ImmutableEquatableArray.Empty<PostgresInterfaceModel>())));

        var diagnostics = parseStep
            .Select(static (x, _) => x.diagnostics.ToImmutableEquatableArray())
            .WithTrackingName(PostgresGeneratorStepName.ReportDiagnostics);
        context.ReportDiagnostics(diagnostics);

        var contextModel = parseStep.Select(static (x, _) => x.model);
        var interfaceModels = contextModel
            .SelectMany(static (x, _) => x.Interfaces)
            .WithTrackingName(PostgresGeneratorStepName.BuildPostgres);
        context.EmitSource(interfaceModels);
        context.EmitModuleInitializers(contextModel);
    }
}
