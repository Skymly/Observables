using Microsoft.CodeAnalysis;

namespace Observables.SourceGenerators.Shared.Diagnostics;

internal static class RoutedEventsDiagnosticDescriptors
{
    private const string Category = "Observables.RoutedEvents.R3.SourceGenerators";

    public static readonly DiagnosticDescriptor InvalidEventDelegate = new(
        id: "OBS4001",
        title: "Unsupported event delegate",
        messageFormat: "The event '{0}' has an unsupported delegate signature for observable generation.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidEventHandlersDelegate = new(
        id: "OBS4002",
        title: "RoutedEventHandlers requires EventHandler or legacy object-sender delegate shape",
        messageFormat: "The event '{0}' is unsupported for RoutedEventHandlers (needs System.EventHandler, System.EventHandler<T>, or void delegate with (object, T) parameters).",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
