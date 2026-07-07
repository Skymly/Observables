using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Observables.RestAPI.Generators;
using Observables.SourceGenerators.Shared;

namespace Observables.RestAPI.R3.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class RestApiR3InterfaceStubGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidateMethodsProvider = context.SyntaxProvider.CreateSyntaxProvider(
            static (syntax, _) =>
                syntax is MethodDeclarationSyntax { Parent: InterfaceDeclarationSyntax, AttributeLists.Count: > 0 },
            static (ctx, _) => (MethodDeclarationSyntax)ctx.Node);

        var candidateInterfacesProvider = context.SyntaxProvider.CreateSyntaxProvider(
            static (syntax, _) => syntax is InterfaceDeclarationSyntax { BaseList: not null },
            static (ctx, _) => (InterfaceDeclarationSyntax)ctx.Node);

        var inputs = candidateMethodsProvider
            .Collect()
            .Combine(candidateInterfacesProvider.Collect())
            .Select(static (combined, _) => (combined.Left, combined.Right))
            .Combine(context.CompilationProvider)
            .Select(static (combined, _) =>
                (
                    combined.Left.Item1,
                    combined.Left.Item2,
                    compilation: combined.Right));

        var parseStep = inputs.Select(
            static (collected, ct) =>
                GeneratorFailSafe.ExecuteParse(
                    () => Parser.GenerateInterfaceStubs(
                        (CSharpCompilation)collected.compilation,
                        collected.Item1,
                        collected.Item2,
                        ct),
                    DiagnosticDescriptors.InternalGeneratorError,
                    () => new ContextGenerationModel(ImmutableEquatableArray.Empty<InterfaceModel>())));

        var diagnostics = parseStep
            .Select(static (x, _) => x.diagnostics.ToImmutableEquatableArray())
            .WithTrackingName(RestApiGeneratorStepName.ReportDiagnostics);
        context.ReportDiagnostics(diagnostics);

        var contextModel = parseStep.Select(static (x, _) => x.Item2);
        var interfaceModels = contextModel
            .SelectMany(static (x, _) => x.Interfaces)
            .WithTrackingName(RestApiGeneratorStepName.BuildRestApi);
        context.EmitSource(interfaceModels);
        context.EmitSharedCode(contextModel);
    }
}
