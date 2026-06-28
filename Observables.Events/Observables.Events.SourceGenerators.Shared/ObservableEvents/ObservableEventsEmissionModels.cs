using System.Collections.Immutable;
using Observables.SourceGenerators.Shared;

namespace Observables.Events.Generators;

/// <summary>
/// Pre-computed, value-comparable emission model covering all entry kinds
/// (Events, EventHandlers, RoutedEvents, RoutedEventHandlers, AttachedRouted*).
/// All Roslyn symbol data is resolved to strings during the parse phase
/// so that Roslyn's incremental engine can short-circuit unchanged outputs.
/// </summary>
internal sealed record EventsEmissionModel(
    ImmutableEquatableArray<EventInterfaceEmissionModel> Interfaces,
    ImmutableEquatableArray<TypeImplEmissionModel> TypeImplementations,
    ImmutableEquatableArray<GenericConstraintEmissionModel> GenericConstraints,
    ImmutableEquatableArray<AttachedRoutedEmissionModel> AttachedRoutedEvents,
    ImmutableEquatableArray<EventsDiagnosticModel> Diagnostics);

internal sealed record EventInterfaceEmissionModel(
    string FileName,
    string Source);

internal sealed record TypeImplEmissionModel(
    string FileName,
    string Source);

internal sealed record GenericConstraintEmissionModel(
    string FileName,
    string Source);

internal sealed record AttachedRoutedEmissionModel(
    string FileName,
    string Source);

internal sealed record EventsDiagnosticModel(
    string DescriptorId,
    string? LocationFilePath,
    int LocationStartLine,
    int LocationStartColumn,
    string MessageArg);
