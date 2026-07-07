using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Observables.SignalR.Generators;
using Observables.SourceGenerators.Shared;

namespace Observables.SignalR.Reactive.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class HubInterfaceStubGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidateInterfaces = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Observables.SignalR.HubAttribute",
            static (node, _) => node is InterfaceDeclarationSyntax,
            static (ctx, _) => (InterfaceDeclarationSyntax)ctx.TargetNode);

        var pipeline = candidateInterfaces
            .Collect()
            .Combine(context.CompilationProvider)
            .Select(static (pair, _) => (pair.Left, pair.Right));

        var parseStep = pipeline.Select(
            static (input, ct) =>
                GeneratorFailSafe.ExecuteParse(
                    () => Parser.GenerateHubStubs((CSharpCompilation)input.Right, input.Left, ct),
                    DiagnosticDescriptors.InternalGeneratorError,
                    () => new ContextGenerationModel(ImmutableEquatableArray.Empty<HubInterfaceModel>())));

        var diagnostics = parseStep
            .Select(static (x, _) => x.diagnostics.ToImmutableEquatableArray())
            .WithTrackingName(SignalRGeneratorStepName.ReportDiagnostics);
        context.ReportDiagnostics(diagnostics);

        var contextModel = parseStep.Select(static (x, _) => x.model);
        var interfaceModels = contextModel
            .SelectMany(static (x, _) => x.Interfaces)
            .WithTrackingName(SignalRGeneratorStepName.BuildSignalR);
        context.EmitSource(interfaceModels);
        context.EmitModuleInitializers(contextModel);
    }
}
