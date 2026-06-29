using Microsoft.CodeAnalysis;

namespace Observables.Events.Generators;

internal static class DiagnosticDescriptors
{
    private const string Category = "Observables.Events";

    public static readonly DiagnosticDescriptor InvalidEventDelegate = new(
        id: "OBS2001",
        title: "Unsupported event delegate",
        messageFormat: "The event '{0}' has an unsupported delegate signature for observable generation",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidEventHandlersDelegate = new(
        id: "OBS2002",
        title: "EventHandlers requires EventHandler or legacy object-sender delegate shape",
        messageFormat: "The event '{0}' is unsupported for EventHandlers and must use EventHandler or object-sender delegate shape",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidRoutedEventDelegate = new(
        id: "OBS2003",
        title: "Unsupported routed event delegate",
        messageFormat: "The event '{0}' has an unsupported delegate signature for routed observable generation",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidRoutedEventHandlersDelegate = new(
        id: "OBS2004",
        title: "RoutedEventHandlers requires EventHandler or legacy object-sender delegate shape",
        messageFormat: "The event '{0}' is unsupported for RoutedEventHandlers and must use EventHandler or object-sender delegate shape",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
