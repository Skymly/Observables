using Microsoft.CodeAnalysis;
using Observables.SourceGenerators.Shared;

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
        isEnabledByDefault: true,
        description: "Classic Events() — unsupported event delegate signature.",
        helpLinkUri: DiagnosticHelpLink.For("OBS2001"));

    public static readonly DiagnosticDescriptor InvalidEventHandlersDelegate = new(
        id: "OBS2002",
        title: "EventHandlers requires EventHandler or legacy object-sender delegate shape",
        messageFormat: "The event '{0}' is unsupported for EventHandlers and must use EventHandler or object-sender delegate shape",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "EventHandlers() — not EventHandler or (object, T) shape.",
        helpLinkUri: DiagnosticHelpLink.For("OBS2002"));

    public static readonly DiagnosticDescriptor InvalidRoutedEventDelegate = new(
        id: "OBS2003",
        title: "Unsupported routed event delegate",
        messageFormat: "The event '{0}' has an unsupported delegate signature for routed observable generation",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "RoutedEvents() — unsupported routed event delegate.",
        helpLinkUri: DiagnosticHelpLink.For("OBS2003"));

    public static readonly DiagnosticDescriptor InvalidRoutedEventHandlersDelegate = new(
        id: "OBS2004",
        title: "RoutedEventHandlers requires EventHandler or legacy object-sender delegate shape",
        messageFormat: "The event '{0}' is unsupported for RoutedEventHandlers and must use EventHandler or object-sender delegate shape",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "RoutedEventHandlers() — unsupported routed handler delegate.",
        helpLinkUri: DiagnosticHelpLink.For("OBS2004"));

    public static readonly DiagnosticDescriptor InternalGeneratorError = new(
        id: "OBS2005",
        title: "Internal source generator error",
        messageFormat: "An internal error occurred in the Events source generator: {0}: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Unexpected internal failure in the Events source generator.",
        helpLinkUri: DiagnosticHelpLink.For("OBS2005"));
}
