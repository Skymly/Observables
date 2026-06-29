using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Observables.SourceGenerators.Shared;
using Observables.SourceGenerators.Shared.Extensions;

namespace Observables.Events.Generators;

[Generator(LanguageNames.CSharp)]
public sealed partial class ObservableEventsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource(
                $"{ObservableEventsConstants.GeneratedNamespace}.ObservableEventsBootstrapExtensions.g.cs",
                SourceText.From(
                    GeneratedSourceHeader.ToSource(
                        EventsBootstrapSyntaxFactory.CreateClassicObservableEventsBootstrapExtensionsCompilationUnit(
                            ObservableEventsConstants.StaticObservableEventsGenerationEnabled)),
                    Encoding.UTF8));
            ctx.AddSource(
                $"{ObservableEventsConstants.GeneratedNamespace}.NullEvents.g.cs",
                SourceText.From(
                    GeneratedSourceHeader.ToSource(EventsBootstrapSyntaxFactory.CreateNullEventsCompilationUnit()),
                    Encoding.UTF8));
#if EVENTS_R3
            ctx.AddSource(
                $"{ObservableEventsConstants.GeneratedNamespace}.EventObservable.g.cs",
                SourceText.From(
                    GeneratedSourceHeader.ToSource(EventsBootstrapSyntaxFactory.CreateEventObservableBridgeCompilationUnit()),
                    Encoding.UTF8));
#endif
        });
        RegisterObservableEventsStaticsShellPostInit(context);

        var observableEventsCandidates = context.SyntaxProvider.CreateSyntaxProvider(
            static (syntax, _) => IsObservableEventsInstanceEntryInvocation(syntax)
                || (ObservableEventsConstants.StaticObservableEventsGenerationEnabled && IsStaticEventsEntryMemberAccess(syntax)),
            static (syntaxContext, _) => syntaxContext.Node);

        var analyzerConfig = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
        {
            var globalOptions = provider.GlobalOptions;
            var useWpf = globalOptions.TryGetValue("build_property.UseWPF", out var useWpfRaw)
                && (string.Equals(useWpfRaw, "true", System.StringComparison.OrdinalIgnoreCase)
                    || (bool.TryParse(useWpfRaw, out var useWpfBool) && useWpfBool));
            var observableRoutedEvents = globalOptions.TryGetValue("build_property.ObservableRoutedEvents", out var routedRaw)
                && (string.Equals(routedRaw, "true", System.StringComparison.OrdinalIgnoreCase)
                    || (bool.TryParse(routedRaw, out var routedBool) && routedBool));
            return (UseWpf: useWpf, ObservableRoutedEvents: observableRoutedEvents);
        });

        var pipeline = observableEventsCandidates.Collect()
            .Combine(context.CompilationProvider)
            .Combine(analyzerConfig)
            .Select(static (triple, _) =>
            {
                var candidates = triple.Left.Left;
                var compilation = triple.Left.Right;
                var config = triple.Right;
                var observableRoutedEvents = config.ObservableRoutedEvents
                    || IsGeneratorTestRoutedGeneration(compilation, candidates);
                return (
                    Candidates: candidates,
                    Compilation: compilation,
                    UseWpf: config.UseWpf,
                    ObservableRoutedEvents: observableRoutedEvents);
            });

        var parseStep = pipeline
            .Select(static (input, _) =>
            {
                var model = ParseEvents(input.Compilation, input.Candidates, input.UseWpf, input.ObservableRoutedEvents);
                return (input.ObservableRoutedEvents, Model: model);
            })
            .WithTrackingName(EventsGeneratorStepName);

        context.RegisterSourceOutput(parseStep, (spc, tuple) =>
        {
            var (observableRoutedEvents, model) = tuple;

            if (observableRoutedEvents)
            {
                spc.AddSource(
                    $"{ObservableEventsConstants.GeneratedNamespace}.ObservableEventsBootstrapExtensions.Routed.g.cs",
                    SourceText.From(
                        GeneratedSourceHeader.ToSource(
                            EventsBootstrapSyntaxFactory.CreateRoutedObservableEventsBootstrapExtensionsCompilationUnit()),
                        Encoding.UTF8));
            }

            // Report captured diagnostics
            foreach (var diag in model.Diagnostics)
            {
                var descriptor = diag.DescriptorId switch
                {
                    "OBS2001" => DiagnosticDescriptors.InvalidEventDelegate,
                    "OBS2002" => DiagnosticDescriptors.InvalidEventHandlersDelegate,
                    "OBS2003" => DiagnosticDescriptors.InvalidRoutedEventDelegate,
                    "OBS2004" => DiagnosticDescriptors.InvalidRoutedEventHandlersDelegate,
                    _ => null,
                };
                if (descriptor is null) continue;

                var location = diag.LocationFilePath is not null
                    ? Microsoft.CodeAnalysis.Location.Create(
                        diag.LocationFilePath,
                        new Microsoft.CodeAnalysis.Text.TextSpan(0, 0),
                        new Microsoft.CodeAnalysis.Text.LinePositionSpan(
                            new(diag.LocationStartLine, diag.LocationStartColumn),
                            new(diag.LocationStartLine, diag.LocationStartColumn)))
                    : Microsoft.CodeAnalysis.Location.None;
                spc.ReportDiagnostic(Diagnostic.Create(descriptor, location, diag.MessageArg));
            }

            foreach (var iface in model.Interfaces)
                spc.AddSource(iface.FileName, SourceText.From(iface.Source, Encoding.UTF8));

            foreach (var impl in model.TypeImplementations)
                spc.AddSource(impl.FileName, SourceText.From(impl.Source, Encoding.UTF8));

            foreach (var gc in model.GenericConstraints)
                spc.AddSource(gc.FileName, SourceText.From(gc.Source, Encoding.UTF8));

            foreach (var ar in model.AttachedRoutedEvents)
                spc.AddSource(ar.FileName, SourceText.From(ar.Source, Encoding.UTF8));
        });
    }

    private static bool IsGeneratorTestRoutedGeneration(Compilation compilation, ImmutableArray<SyntaxNode> candidates)
    {
        if (string.Equals(compilation.AssemblyName, "Observables.Samples.Events.Routed", System.StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.Equals(compilation.AssemblyName, "GeneratorTests", System.StringComparison.Ordinal))
        {
            return false;
        }

        return candidates.Any(static c => c is InvocationExpressionSyntax invocation
            && IsRoutedEntryInvocationName(invocation));
    }

    private static bool IsRoutedEntryInvocationName(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        return memberAccess.Name.Identifier.ValueText is ObservableEventsConstants.RoutedEventsEntryMethodName
            or ObservableEventsConstants.RoutedEventHandlersEntryMethodName
            or ObservableEventsConstants.AttachedRoutedEventEntryMethodName
            or ObservableEventsConstants.AttachedRoutedEventHandlerEntryMethodName;
    }
}
