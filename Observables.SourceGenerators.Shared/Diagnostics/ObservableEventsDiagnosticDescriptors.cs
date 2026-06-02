using Microsoft.CodeAnalysis;

namespace Observables.SourceGenerators.Shared.Diagnostics;

internal static class ObservableEventsDiagnosticDescriptors
{
    private const string Category = "Observables.Events.R3.SourceGenerators";

    public static readonly DiagnosticDescriptor InvalidEventDelegate = new(
        id: "OBS2001",
        title: "Unsupported event delegate",
        messageFormat: "The event '{0}' has an unsupported delegate signature for observable generation.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidEventHandlersDelegate = new(
        id: "OBS2002",
        title: "EventHandlers requires EventHandler or legacy object-sender delegate shape",
        messageFormat: "The event '{0}' is unsupported for EventHandlers (needs System.EventHandler, System.EventHandler<T>, or void delegate with (object, T) parameters).",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
