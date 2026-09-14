using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Observables.Roslyn.Shared;
using Observables.SourceGenerators.Shared;

namespace Observables.SignalR.Generators;

internal static class SignalRGeneratorPipeline
{
    internal const string FailSafeProbePropertyName = "build_property.ObservablesSignalRFailSafeProbe";

    internal static bool IsFailSafeProbeEnabled(AnalyzerConfigOptionsProvider options) =>
        options.GlobalOptions.TryGetValue(FailSafeProbePropertyName, out var raw)
        && (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || (bool.TryParse(raw, out var parsed) && parsed));

    internal static void Register(IncrementalGeneratorInitializationContext context)
    {
        var domain = ProxyDomainTable.SignalR;
        var candidateInterfaces = context.SyntaxProvider.ForAttributeWithMetadataName(
            domain.InterfaceMarkerMetadataName,
            static (node, _) => node is InterfaceDeclarationSyntax,
            static (ctx, _) => (InterfaceDeclarationSyntax)ctx.TargetNode);

        var collected = candidateInterfaces
            .Collect()
            .Combine(context.CompilationProvider)
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (pair, _) => (
                Interfaces: pair.Left.Left,
                Compilation: pair.Left.Right,
                Options: pair.Right));

        var parseStep = collected.Select(
            static (input, ct) =>
                GeneratorFailSafe.ExecuteParse(
                    () =>
                    {
                        var compilation = (CSharpCompilation)input.Compilation;
                        var markerName = ProxyDomainTable.SignalR.InterfaceMarkerMetadataName;
                        var marker = compilation.GetTypeByMetadataName(markerName);
                        var marked = marker is null
                            ? ImmutableArray<MarkedInterfaceContext>.Empty
                            : IoProxyInterfaceWalk.Collect(compilation, input.Interfaces, marker, ct);
                        return Parser.GenerateHubStubs(
                            compilation,
                            marked,
                            ct,
                            IsFailSafeProbeEnabled(input.Options));
                    },
                    DiagnosticDescriptors.InternalGeneratorError,
                    static () => new ContextGenerationModel(ImmutableEquatableArray.Empty<HubInterfaceModel>())));

        var diagnostics = parseStep
            .Select(static (x, _) => x.diagnostics.ToImmutableEquatableArray())
            .WithTrackingName(SignalRGeneratorStepName.ReportDiagnostics);
        context.ReportDiagnostics(diagnostics);

        var contextModel = parseStep.Select(static (x, _) => x.model);
        var interfaceModels = contextModel
            .SelectMany(static (model, _) => model.Interfaces)
            .WithTrackingName(SignalRGeneratorStepName.BuildSignalR);

        context.RegisterImplementationSourceOutput(
            interfaceModels,
            static (spc, model) =>
                GeneratorFailSafe.TryEmit(
                    () => spc.AddSource(model.FileName, Emitter.EmitInterface(model)),
                    spc.ReportDiagnostic,
                    DiagnosticDescriptors.InternalGeneratorError));

        context.RegisterImplementationSourceOutput(
            contextModel,
            static (spc, model) =>
                GeneratorFailSafe.TryEmit(
                    () => Emitter.EmitModuleInitializers(model, (name, code) => spc.AddSource(name, code)),
                    spc.ReportDiagnostic,
                    DiagnosticDescriptors.InternalGeneratorError));
    }
}
