using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
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
                        EventsBootstrapSyntaxFactory.CreateObservableEventsBootstrapExtensionsCompilationUnit(
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
                || (ObservableEventsConstants.StaticObservableEventsGenerationEnabled && IsStaticFromEventsEntryMemberAccess(syntax)),
            static (syntaxContext, _) => syntaxContext.Node);

        var inputs = observableEventsCandidates.Collect()
            .Combine(context.CompilationProvider)
            .Select(static (pair, _) => (Candidates: pair.Left, Compilation: pair.Right));

        context.RegisterSourceOutput(inputs, static (spc, input) =>
        {
            var targets = CollectObservableEventTargets(input.Compilation, input.Candidates);

            EmitInterfaceBasedSources(
                targets.FromEventsTypes,
                targets.FromEventsGenericConstraintTargets,
                input.Compilation, spc, ObservableEventsEntryKind.FromEvents);

            EmitInterfaceBasedSources(
                targets.FromEventHandlersTypes,
                targets.FromEventHandlersGenericConstraintTargets,
                input.Compilation, spc, ObservableEventsEntryKind.FromEventHandlers);
        });
    }
}
