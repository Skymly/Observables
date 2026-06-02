using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Observables.SourceGenerators.Shared;
using Observables.SourceGenerators.Shared.Extensions;

namespace Observables.Events.Reactive.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed partial class ObservableEventsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource(
                "Observables.Events.Reactive.ObservableEventsBootstrapExtensions.g.cs",
                SourceText.From(
                    GeneratedSourceHeader.ToSource(
                        EventsBootstrapSyntaxFactory.CreateClassicObservableEventsBootstrapExtensionsCompilationUnit(
                            ObservableEventsConstants.StaticObservableEventsGenerationEnabled)),
                    Encoding.UTF8));
            ctx.AddSource(
                "Observables.Events.Reactive.NullEvents.g.cs",
                SourceText.From(
                    GeneratedSourceHeader.ToSource(EventsBootstrapSyntaxFactory.CreateNullEventsCompilationUnit()),
                    Encoding.UTF8));
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

        context.RegisterSourceOutput(pipeline, (spc, input) =>
        {
            if (input.ObservableRoutedEvents)
            {
                spc.AddSource(
                    "Observables.Events.Reactive.ObservableEventsBootstrapExtensions.Routed.g.cs",
                    SourceText.From(
                        GeneratedSourceHeader.ToSource(
                            EventsBootstrapSyntaxFactory.CreateRoutedObservableEventsBootstrapExtensionsCompilationUnit()),
                        Encoding.UTF8));
            }

            var targets = CollectObservableEventTargets(
                input.Compilation,
                input.Candidates,
                input.UseWpf,
                input.ObservableRoutedEvents);

            EmitInterfaceBasedSources(
                targets.EventsTypes,
                targets.EventsGenericConstraintTargets,
                input.Compilation,
                spc,
                ObservableEventsEntryKind.Events);

            EmitInterfaceBasedSources(
                targets.EventHandlersTypes,
                targets.EventHandlersGenericConstraintTargets,
                input.Compilation,
                spc,
                ObservableEventsEntryKind.EventHandlers);

            if (!input.ObservableRoutedEvents)
            {
                return;
            }

            EmitInterfaceBasedSources(
                targets.RoutedEventsTypes,
                ImmutableArray<GenericConstraintTarget>.Empty,
                input.Compilation,
                spc,
                ObservableEventsEntryKind.RoutedEvents,
                input.UseWpf);

            EmitInterfaceBasedSources(
                targets.RoutedEventHandlersTypes,
                ImmutableArray<GenericConstraintTarget>.Empty,
                input.Compilation,
                spc,
                ObservableEventsEntryKind.RoutedEventHandlers,
                input.UseWpf);

            foreach (var target in targets.AttachedRoutedEventsTypes)
            {
                var source = GenerateAttachedRoutedEventSourceForTarget(target, ObservableEventsEntryKind.AttachedRoutedEvent);
                if (!string.IsNullOrWhiteSpace(source))
                {
                    spc.AddSource($"{target.ReceiverType.GetSafeHintName()}.AttachedRoutedEvent.g.cs", SourceText.From(source, Encoding.UTF8));
                }
            }

            foreach (var target in targets.AttachedRoutedEventHandlersTypes)
            {
                var source = GenerateAttachedRoutedEventSourceForTarget(target, ObservableEventsEntryKind.AttachedRoutedEventHandler);
                if (!string.IsNullOrWhiteSpace(source))
                {
                    spc.AddSource($"{target.ReceiverType.GetSafeHintName()}.AttachedRoutedEventHandler.g.cs", SourceText.From(source, Encoding.UTF8));
                }
            }
        });
    }

    private static bool IsGeneratorTestRoutedGeneration(Compilation compilation, ImmutableArray<SyntaxNode> candidates)
    {
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