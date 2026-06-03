using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Observables.SignalR.Generators;

namespace Observables.SignalR.R3.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class HubInterfaceStubGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidateInterfaces = context.SyntaxProvider.CreateSyntaxProvider(
            static (syntax, _) => syntax is InterfaceDeclarationSyntax { AttributeLists.Count: > 0 },
            static (ctx, _) => (InterfaceDeclarationSyntax)ctx.Node);

        var pipeline = candidateInterfaces
            .Collect()
            .Combine(context.CompilationProvider)
            .Select(static (pair, _) => (pair.Left, pair.Right));

        var parseStep = pipeline.Select(
            static (input, ct) =>
                Parser.GenerateHubStubs((CSharpCompilation)input.Right, input.Left, ct));

        var diagnostics = parseStep
            .Select(static (x, _) => x.diagnostics.ToImmutableEquatableArray())
            .WithTrackingName(SignalRGeneratorStepName.ReportDiagnostics);
        context.ReportDiagnostics(diagnostics);

        var contextModel = parseStep.Select(static (x, _) => x.model);
        var interfaceModels = contextModel
            .SelectMany(static (x, _) => x.Interfaces)
            .WithTrackingName(SignalRGeneratorStepName.BuildSignalR);
        context.EmitSource(interfaceModels);

        context.RegisterImplementationSourceOutput(
            contextModel,
            static (spc, model) => Emitter.EmitModuleInitializers(model, (name, code) => spc.AddSource(name, code)));
    }
}
