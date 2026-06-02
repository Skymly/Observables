using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Observables.RestAPI.Generators;

namespace Observables.RestAPI.Reactive.SourceGenerators;

[Generator]
public sealed class RestApiInterfaceStubGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidateMethodsProvider = context.SyntaxProvider.CreateSyntaxProvider(
            static (syntax, _) =>
                syntax
                    is MethodDeclarationSyntax
                    {
                        Parent: InterfaceDeclarationSyntax,
                        AttributeLists.Count: > 0
                    },
            static (syntaxContext, _) => (MethodDeclarationSyntax)syntaxContext.Node
        );

        var candidateInterfacesProvider = context.SyntaxProvider.CreateSyntaxProvider(
            static (syntax, _) => syntax is InterfaceDeclarationSyntax { BaseList: not null },
            static (syntaxContext, _) => (InterfaceDeclarationSyntax)syntaxContext.Node
        );

        var restApiInternalNamespace = context.AnalyzerConfigOptionsProvider.Select(
            static (analyzerConfigOptionsProvider, _) =>
                analyzerConfigOptionsProvider.GlobalOptions.TryGetValue(
                    "build_property.RestApiInternalNamespace",
                    out var value
                )
                    ? value
                    : null
        );

        var inputs = candidateMethodsProvider
            .Collect()
            .Combine(candidateInterfacesProvider.Collect())
            .Select(
                static (combined, _) =>
                    (candidateMethods: combined.Left, candidateInterfaces: combined.Right)
            )
            .Combine(restApiInternalNamespace)
            .Combine(context.CompilationProvider)
            .Select(
                static (combined, _) =>
                    (
                        combined.Left.Left.candidateMethods,
                        combined.Left.Left.candidateInterfaces,
                        RestApiInternalNamespace: combined.Left.Right,
                        compilation: combined.Right
                    )
            );

        var parseStep = inputs.Select(
            static (collectedValues, cancellationToken) =>
                Parser.GenerateInterfaceStubs(
                    (CSharpCompilation)collectedValues.compilation,
                    collectedValues.RestApiInternalNamespace,
                    collectedValues.candidateMethods,
                    collectedValues.candidateInterfaces,
                    cancellationToken
                )
        );

        var diagnostics = parseStep
            .Select(static (x, _) => x.diagnostics.ToImmutableEquatableArray())
            .WithTrackingName(RestApiGeneratorStepName.ReportDiagnostics);
        context.ReportDiagnostics(diagnostics);

        var contextModel = parseStep.Select(static (x, _) => x.Item2);
        var interfaceModels = contextModel
            .SelectMany(static (x, _) => x.Interfaces)
            .WithTrackingName(RestApiGeneratorStepName.BuildRestApi);
        context.EmitSource(interfaceModels);

        context.RegisterImplementationSourceOutput(
            contextModel,
            static (spc, model) =>
            {
                Emitter.EmitSharedCode(model, (name, code) => spc.AddSource(name, code));
            }
        );
    }
}
