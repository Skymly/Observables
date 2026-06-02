using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Observables.SourceGenerators.Shared;
using Observables.SourceGenerators.Shared.Extensions;

namespace Observables.RoutedEvents.Reactive.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed partial class ObservableEventsGenerator : IIncrementalGenerator
{
public void Initialize(IncrementalGeneratorInitializationContext context)
{
    context.RegisterPostInitializationOutput(static ctx =>
    {
        ctx.AddSource(
            "Observables.RoutedEvents.Reactive.ObservableEventsBootstrapExtensions.g.cs",
            SourceText.From(
                GeneratedSourceHeader.ToSource(
                    EventsBootstrapSyntaxFactory.CreateObservableEventsBootstrapExtensionsCompilationUnit(
                        ObservableEventsConstants.StaticObservableEventsGenerationEnabled)),
                Encoding.UTF8));
        ctx.AddSource(
            "Observables.RoutedEvents.Reactive.NullEvents.g.cs",
            SourceText.From(
                GeneratedSourceHeader.ToSource(EventsBootstrapSyntaxFactory.CreateNullEventsCompilationUnit()),
                Encoding.UTF8));
        ctx.AddSource(
            "Observables.RoutedEvents.Reactive.EventObservable.g.cs",
            SourceText.From(
                GeneratedSourceHeader.ToSource(EventsBootstrapSyntaxFactory.CreateEventObservableBridgeCompilationUnit()),
                Encoding.UTF8));
    });
    RegisterObservableEventsStaticsShellPostInit(context);

    var observableEventsCandidates = context.SyntaxProvider.CreateSyntaxProvider(
        static (syntax, _) => IsObservableEventsInstanceEntryInvocation(syntax)
            || (ObservableEventsConstants.StaticObservableEventsGenerationEnabled && IsStaticEventsEntryMemberAccess(syntax)),
        static (syntaxContext, _) => syntaxContext.Node);

    var inputs = observableEventsCandidates.Collect()
        .Combine(context.CompilationProvider)
        .Combine(context.AnalyzerConfigOptionsProvider)
        .Select(static (triple, _) =>
        {
            var candidates = triple.Left.Left;
            var compilation = triple.Left.Right;
            var analyzerConfig = triple.Right;
            var useWpf = analyzerConfig.GlobalOptions.TryGetValue("build_property.UseWPF", out var useWpfRaw)
                && (string.Equals(useWpfRaw, "true", System.StringComparison.OrdinalIgnoreCase)
                    || (bool.TryParse(useWpfRaw, out var useWpfBool) && useWpfBool));
            return (Candidates: candidates, Compilation: compilation, UseWpf: useWpf);
        });

    context.RegisterSourceOutput(inputs, static (spc, input) =>
    {
        var targets = CollectObservableEventTargets(input.Compilation, input.Candidates, input.UseWpf);

        EmitInterfaceBasedSources(
            targets.RoutedEventsTypes,
            ImmutableArray<GenericConstraintTarget>.Empty,
            input.Compilation, spc, ObservableEventsEntryKind.RoutedEvents, input.UseWpf);

        EmitInterfaceBasedSources(
            targets.RoutedEventHandlersTypes,
            ImmutableArray<GenericConstraintTarget>.Empty,
            input.Compilation, spc, ObservableEventsEntryKind.RoutedEventHandlers, input.UseWpf);

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
}
