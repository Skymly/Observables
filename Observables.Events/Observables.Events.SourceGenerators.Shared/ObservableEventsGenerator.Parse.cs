using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Observables.SourceGenerators.Shared;
using Observables.SourceGenerators.Shared.Diagnostics;
using Observables.SourceGenerators.Shared.Extensions;

namespace Observables.Events.Generators;

public sealed partial class ObservableEventsGenerator
{
    internal static readonly string EventsGeneratorStepName = $"{nameof(ObservableEventsGenerator)}.Parse";

    /// <summary>
    /// Parse step: transforms Roslyn symbols into a value-comparable <see cref="EventsEmissionModel"/>.
    /// All compilation-dependent work (symbol resolution, hierarchy building, source generation)
    /// happens here so the incremental engine can cache the result.
    /// </summary>
    private static EventsEmissionModel ParseEvents(
        Compilation compilation,
        ImmutableArray<SyntaxNode> candidates,
        bool useWpf,
        bool observableRoutedEvents)
    {
        var targets = CollectObservableEventTargets(
            compilation,
            candidates,
            useWpf,
            observableRoutedEvents);

        var diagnostics = new List<EventsDiagnosticModel>();
        var interfaces = new List<EventInterfaceEmissionModel>();
        var typeImpls = new List<TypeImplEmissionModel>();
        var genericConstraints = new List<GenericConstraintEmissionModel>();
        var attachedRouted = new List<AttachedRoutedEmissionModel>();

        // Events entry kind
        CollectEmissionSources(
            targets.EventsTypes,
            targets.EventsGenericConstraintTargets,
            compilation,
            ObservableEventsEntryKind.Events,
            useWpf: false,
            diagnostics,
            interfaces,
            typeImpls,
            genericConstraints);

        // EventHandlers entry kind
        CollectEmissionSources(
            targets.EventHandlersTypes,
            targets.EventHandlersGenericConstraintTargets,
            compilation,
            ObservableEventsEntryKind.EventHandlers,
            useWpf: false,
            diagnostics,
            interfaces,
            typeImpls,
            genericConstraints);

        if (observableRoutedEvents)
        {
            // RoutedEvents entry kind
            CollectEmissionSources(
                targets.RoutedEventsTypes,
                ImmutableArray<GenericConstraintTarget>.Empty,
                compilation,
                ObservableEventsEntryKind.RoutedEvents,
                useWpf,
                diagnostics,
                interfaces,
                typeImpls,
                genericConstraints);

            // RoutedEventHandlers entry kind
            CollectEmissionSources(
                targets.RoutedEventHandlersTypes,
                ImmutableArray<GenericConstraintTarget>.Empty,
                compilation,
                ObservableEventsEntryKind.RoutedEventHandlers,
                useWpf,
                diagnostics,
                interfaces,
                typeImpls,
                genericConstraints);

            // Attached routed events
            foreach (var target in targets.AttachedRoutedEventsTypes)
            {
                var source = GenerateAttachedRoutedEventSourceForTarget(target, ObservableEventsEntryKind.AttachedRoutedEvent);
                if (!string.IsNullOrWhiteSpace(source))
                {
                    attachedRouted.Add(new AttachedRoutedEmissionModel(
                        $"{target.ReceiverType.GetSafeHintName()}.AttachedRoutedEvent.g.cs",
                        source));
                }
            }

            foreach (var target in targets.AttachedRoutedEventHandlersTypes)
            {
                var source = GenerateAttachedRoutedEventSourceForTarget(target, ObservableEventsEntryKind.AttachedRoutedEventHandler);
                if (!string.IsNullOrWhiteSpace(source))
                {
                    attachedRouted.Add(new AttachedRoutedEmissionModel(
                        $"{target.ReceiverType.GetSafeHintName()}.AttachedRoutedEventHandler.g.cs",
                        source));
                }
            }
        }

        return new EventsEmissionModel(
            interfaces.ToImmutableEquatableArray(),
            typeImpls.ToImmutableEquatableArray(),
            genericConstraints.ToImmutableEquatableArray(),
            attachedRouted.ToImmutableEquatableArray(),
            diagnostics.ToImmutableEquatableArray());
    }

    private static void CollectEmissionSources(
        ImmutableArray<INamedTypeSymbol> callSiteTypes,
        ImmutableArray<GenericConstraintTarget> genericConstraintTargets,
        Compilation compilation,
        ObservableEventsEntryKind entryKind,
        bool useWpf,
        List<EventsDiagnosticModel> diagnostics,
        List<EventInterfaceEmissionModel> interfaces,
        List<TypeImplEmissionModel> typeImpls,
        List<GenericConstraintEmissionModel> genericConstraints)
    {
        var allTypes = callSiteTypes.AddRange(
            genericConstraintTargets.SelectMany(static t => t.ConstraintTypes)
                .Select(static t => t.IsGenericType ? (INamedTypeSymbol)t.OriginalDefinition : t));

        var hierarchy = BuildEventInterfaceHierarchy(allTypes, entryKind, compilation, useWpf);
        if (hierarchy.Count == 0 && genericConstraintTargets.Length == 0)
            return;

        var kindTag = GetEntryKindSourceTag(entryKind);

        if (hierarchy.Count > 0)
        {
            var interfacesSource = GenerateEventInterfacesSource(hierarchy, compilation, entryKind);
            if (!string.IsNullOrWhiteSpace(interfacesSource))
            {
                interfaces.Add(new EventInterfaceEmissionModel(
                    $"EventInterfaces.{kindTag}.g.cs",
                    interfacesSource));
            }
        }

        var capturedDiagnostics = new List<(string DescriptorId, Location? Location, string MessageArg)>();
        Action<string, Location?, string> reportDiagnostic = (id, loc, arg) =>
            capturedDiagnostics.Add((id, loc, arg));

        foreach (var type in callSiteTypes)
        {
            var source = GenerateEventImplAndExtensionSource(
                type, hierarchy, compilation, reportDiagnostic, entryKind);
            if (!string.IsNullOrWhiteSpace(source))
            {
                typeImpls.Add(new TypeImplEmissionModel(
                    $"{type.GetSafeHintName()}.{kindTag}.g.cs",
                    source));
            }
        }

        foreach (var target in genericConstraintTargets)
        {
            var source = GenerateGenericConstraintEventSource(
                target, hierarchy, compilation, reportDiagnostic, entryKind);
            if (!string.IsNullOrWhiteSpace(source))
            {
                genericConstraints.Add(new GenericConstraintEmissionModel(
                    $"{GetGenericConstraintTargetHintName(target)}.{kindTag}.g.cs",
                    source));
            }
        }

        foreach (var (id, loc, arg) in capturedDiagnostics)
        {
            diagnostics.Add(ToDiagnosticModel(id, loc, arg));
        }
    }

    private static EventsDiagnosticModel ToDiagnosticModel(string descriptorId, Location? loc, string messageArg)
    {
        string? filePath = null;
        int line = 0, col = 0;
        if (loc is not null && loc.IsInSource && loc.SourceTree is not null)
        {
            filePath = loc.SourceTree.FilePath;
            var lineSpan = loc.GetLineSpan();
            line = lineSpan.StartLinePosition.Line;
            col = lineSpan.StartLinePosition.Character;
        }
        return new EventsDiagnosticModel(
            descriptorId,
            filePath,
            line,
            col,
            messageArg);
    }
}
