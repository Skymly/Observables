#if ROSLYN_4
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Observables.SourceGenerators.Shared;

/// <summary>
/// Deep pipeline for ForAttributeWithMetadataName interface-proxy stub generators.
/// Domains supply marker name, parse, emit delegates, and tracking names as adapters.
/// RestAPI (CreateSyntaxProvider) and Events are out of scope.
/// </summary>
internal static class IoProxyGeneratorPipeline
{
    internal static void RegisterForAttributeInterfaces<TContextModel, TInterfaceModel>(
        IncrementalGeneratorInitializationContext context,
        Observables.Roslyn.Shared.ProxyDomainTable.ProxyDomainDefinition domain,
        Func<CSharpCompilation, ImmutableArray<MarkedInterfaceContext>, CancellationToken, (List<Diagnostic> diagnostics, TContextModel model)> parse,
        DiagnosticDescriptor internalErrorDescriptor,
        Func<TContextModel> emptyModelFactory,
        Func<TContextModel, IEnumerable<TInterfaceModel>> getInterfaces,
        string reportDiagnosticsTrackingName,
        string buildInterfacesTrackingName,
        Action<TInterfaceModel, Action<string, SourceText>> emitInterface,
        Action<TContextModel, Action<string, SourceText>> emitModuleInitializers)
    {
        var candidateInterfaces = context.SyntaxProvider.ForAttributeWithMetadataName(
            domain.InterfaceMarkerMetadataName,
            static (node, _) => node is InterfaceDeclarationSyntax,
            static (ctx, _) => (InterfaceDeclarationSyntax)ctx.TargetNode);

        var collected = candidateInterfaces
            .Collect()
            .Combine(context.CompilationProvider)
            .Select(static (pair, _) => (pair.Left, pair.Right));

        var parseStep = collected.Select(
            (input, ct) =>
            {
                IoProxyPipelineMeasurement.CountParse();
                return GeneratorFailSafe.ExecuteParse(
                    () =>
                    {
                        var compilation = (CSharpCompilation)input.Right;
                        var marker = compilation.GetTypeByMetadataName(domain.InterfaceMarkerMetadataName);
                        var marked = marker is null
                            ? ImmutableArray<MarkedInterfaceContext>.Empty
                            : IoProxyInterfaceWalk.Collect(compilation, input.Left, marker, ct);
                        return parse(compilation, marked, ct);
                    },
                    internalErrorDescriptor,
                    emptyModelFactory);
            })
            .WithTrackingName(IoProxyPipelineMeasurement.ParseTrackingName);

        var diagnostics = parseStep
            .Select(static (x, _) => x.diagnostics)
            .WithTrackingName(reportDiagnosticsTrackingName);
        context.ReportDiagnostics(diagnostics);

        var contextModel = parseStep.Select(static (x, _) => x.model);
        var interfaceModels = contextModel
            .SelectMany((model, _) => getInterfaces(model))
            .WithTrackingName(buildInterfacesTrackingName);

        context.RegisterImplementationSourceOutput(
            interfaceModels,
            (spc, model) =>
            {
                IoProxyPipelineMeasurement.CountEmitInterface();
                GeneratorFailSafe.TryEmit(
                    () => emitInterface(model, (name, code) => spc.AddSource(name, code)),
                    spc.ReportDiagnostic,
                    internalErrorDescriptor);
            });

        context.RegisterImplementationSourceOutput(
            contextModel,
            (spc, model) =>
                GeneratorFailSafe.TryEmit(
                    () => emitModuleInitializers(model, (name, code) => spc.AddSource(name, code)),
                    spc.ReportDiagnostic,
                    internalErrorDescriptor));
    }
}

/// <summary>
/// Separates parse executions from emit invocations. An emit cache hit is observed
/// from the tracked build step, not inferred from an unchanged output.
/// </summary>
internal static class IoProxyPipelineMeasurement
{
    internal const string ParseTrackingName = "IoProxy.Parse";

    static int parseInvocations;
    static int emitInterfaceInvocations;

    internal static int ParseInvocations => Volatile.Read(ref parseInvocations);

    internal static int EmitInterfaceInvocations => Volatile.Read(ref emitInterfaceInvocations);

    internal static void Reset()
    {
        Volatile.Write(ref parseInvocations, 0);
        Volatile.Write(ref emitInterfaceInvocations, 0);
    }

    internal static void CountParse() => Interlocked.Increment(ref parseInvocations);

    internal static void CountEmitInterface() => Interlocked.Increment(ref emitInterfaceInvocations);
}
#endif
