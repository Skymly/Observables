using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Observables.SourceGenerators.Shared;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Observables.SourceGenerators.Shared.Diagnostics;
using Observables.SourceGenerators.Shared.Extensions;

namespace Observables.Events.Generators;

public sealed partial class ObservableEventsGenerator
{
    private static bool IsObservableEventsInstanceEntryInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Expression: not GenericNameSyntax,
                    Name.Identifier.ValueText: var methodName,
                },
            })
        {
            return false;
        }

        return methodName is ObservableEventsConstants.EventsEntryMethodName
            or ObservableEventsConstants.EventHandlersEntryMethodName
            or ObservableEventsConstants.RoutedEventsEntryMethodName
            or ObservableEventsConstants.RoutedEventHandlersEntryMethodName
            or ObservableEventsConstants.AttachedRoutedEventEntryMethodName
            or ObservableEventsConstants.AttachedRoutedEventHandlerEntryMethodName;
    }

    private static ObservableEventTargetSets CollectObservableEventTargets(
        Compilation compilation,
        ImmutableArray<SyntaxNode> candidates,
        bool useWpf,
        bool observableRoutedEvents)
    {
        var bootstrapType = compilation.GetTypeByMetadataName(ObservableEventsConstants.BootstrapExtensionsMetadataName);
        if (bootstrapType is null)
        {
            return ObservableEventTargetSets.Empty;
        }

        // Use pooled hash sets for better performance with large candidate sets
        var events = new System.Collections.Generic.HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var eventHandlers = new System.Collections.Generic.HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var routedEvents = new System.Collections.Generic.HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var routedHandlers = new System.Collections.Generic.HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var eventsGenericConstraints = new Dictionary<string, GenericConstraintTarget>(System.StringComparer.Ordinal);
        var eventHandlersGenericConstraints = new Dictionary<string, GenericConstraintTarget>(System.StringComparer.Ordinal);
        var attachedRoutedEvents = new System.Collections.Generic.HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var attachedRoutedHandlers = new System.Collections.Generic.HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        // Cache Avalonia detection to avoid repeated metadata lookups
        var avaloniaRoutedEventType = compilation.GetTypeByMetadataName("Avalonia.Interactivity.RoutedEvent`1");
        var avaloniaRoutedEventTypeNonGeneric = compilation.GetTypeByMetadataName("Avalonia.Interactivity.RoutedEvent");
        var useAvalonia = avaloniaRoutedEventType is not null
            || avaloniaRoutedEventTypeNonGeneric is not null;

        foreach (var candidate in candidates)
        {
            if (candidate is InvocationExpressionSyntax invocation)
            {
                var semanticModel = compilation.GetSemanticModel(invocation.SyntaxTree);
                if (observableRoutedEvents
                    && TryCollectRoutedTargetsFromInvocationSyntax(
                        invocation,
                        semanticModel,
                        useWpf,
                        useAvalonia,
                        routedEvents,
                        routedHandlers,
                        attachedRoutedEvents,
                        attachedRoutedHandlers))
                {
                    continue;
                }

                if (semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol methodSymbol)
                {
                    if (methodSymbol.Name == ObservableEventsConstants.EventsEntryMethodName
                        && TryGetBootstrapObservableEventsExtensionTarget(
                            invocation,
                            semanticModel,
                            methodSymbol,
                            bootstrapType,
                            ObservableEventsConstants.EventsEntryMethodName,
                            out var eventsTarget))
                    {
                        if (eventsTarget.IsGenericType)
                        {
                            eventsTarget = eventsTarget.OriginalDefinition;
                        }

                        events.Add(eventsTarget);
                    }
                    else if (methodSymbol.Name == ObservableEventsConstants.EventsEntryMethodName
                             && TryGetBootstrapGenericConstraintTarget(
                                 invocation,
                                 semanticModel,
                                 methodSymbol,
                                 bootstrapType,
                                 ObservableEventsConstants.EventsEntryMethodName,
                                 out var eventsGenericConstraintTarget))
                    {
                        eventsGenericConstraints[eventsGenericConstraintTarget.Key] = eventsGenericConstraintTarget;
                    }
                    else if (methodSymbol.Name == ObservableEventsConstants.EventHandlersEntryMethodName
                             && TryGetBootstrapObservableEventsExtensionTarget(
                                 invocation,
                                 semanticModel,
                                 methodSymbol,
                                 bootstrapType,
                                 ObservableEventsConstants.EventHandlersEntryMethodName,
                                 out var handlerTarget))
                    {
                        if (handlerTarget.IsGenericType)
                        {
                            handlerTarget = handlerTarget.OriginalDefinition;
                        }

                        eventHandlers.Add(handlerTarget);
                    }
                    else if (methodSymbol.Name == ObservableEventsConstants.EventHandlersEntryMethodName
                             && TryGetBootstrapGenericConstraintTarget(
                                 invocation,
                                 semanticModel,
                                 methodSymbol,
                                 bootstrapType,
                                 ObservableEventsConstants.EventHandlersEntryMethodName,
                                 out var handlerGenericConstraintTarget))
                    {
                        eventHandlersGenericConstraints[handlerGenericConstraintTarget.Key] = handlerGenericConstraintTarget;
                    }
                }

                continue;
            }
        }

        static ImmutableArray<INamedTypeSymbol> Order(System.Collections.Generic.HashSet<INamedTypeSymbol> set) =>
            set
                .OrderBy(static t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), System.StringComparer.Ordinal)
                .ToImmutableArray();

        static ImmutableArray<AttachedRoutedEventTarget> OrderAttached(System.Collections.Generic.HashSet<INamedTypeSymbol> set) =>
            set
                .OrderBy(static t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), System.StringComparer.Ordinal)
                .Select(static t => new AttachedRoutedEventTarget(t))
                .ToImmutableArray();

        static ImmutableArray<GenericConstraintTarget> OrderGeneric(Dictionary<string, GenericConstraintTarget> set) =>
            set
                .OrderBy(static pair => pair.Key, System.StringComparer.Ordinal)
                .Select(static pair => pair.Value)
                .ToImmutableArray();

        return new ObservableEventTargetSets(
            Order(events),
            Order(eventHandlers),
            Order(routedEvents),
            Order(routedHandlers),
            OrderGeneric(eventsGenericConstraints),
            OrderGeneric(eventHandlersGenericConstraints),
            OrderAttached(attachedRoutedEvents),
            OrderAttached(attachedRoutedHandlers));
    }

    private static bool TryCollectRoutedTargetsFromInvocationSyntax(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        bool useWpf,
        bool useAvalonia,
        System.Collections.Generic.HashSet<INamedTypeSymbol> routedEvents,
        System.Collections.Generic.HashSet<INamedTypeSymbol> routedHandlers,
        System.Collections.Generic.HashSet<INamedTypeSymbol> attachedRoutedEvents,
        System.Collections.Generic.HashSet<INamedTypeSymbol> attachedRoutedHandlers)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        if (!TryGetExtensionReceiverType(memberAccess.Expression, semanticModel, out var receiverType))
        {
            return false;
        }

        if (receiverType.IsGenericType)
        {
            receiverType = receiverType.OriginalDefinition;
        }

        switch (methodName)
        {
            case var name when name == ObservableEventsConstants.RoutedEventsEntryMethodName
                               && (useWpf || useAvalonia):
                routedEvents.Add(receiverType);
                return true;
            case var name when name == ObservableEventsConstants.RoutedEventHandlersEntryMethodName
                               && (useWpf || useAvalonia):
                routedHandlers.Add(receiverType);
                return true;
            case var name when name == ObservableEventsConstants.AttachedRoutedEventEntryMethodName
                               && useAvalonia:
                attachedRoutedEvents.Add(receiverType);
                return true;
            case var name when name == ObservableEventsConstants.AttachedRoutedEventHandlerEntryMethodName
                               && useAvalonia:
                attachedRoutedHandlers.Add(receiverType);
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetExtensionReceiverType(ExpressionSyntax receiverSyntax, SemanticModel semanticModel, out INamedTypeSymbol receiverType)
    {
        receiverType = null!;
        if (semanticModel.GetTypeInfo(receiverSyntax).Type is INamedTypeSymbol named)
        {
            receiverType = named;
            return true;
        }

        return false;
    }

    private static bool TryGetBootstrapObservableEventsExtensionTarget(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        IMethodSymbol methodSymbol,
        INamedTypeSymbol bootstrapType,
        string entryMethodName,
        out INamedTypeSymbol namedType)
    {
        namedType = null!;

        if (!string.Equals(methodSymbol.Name, entryMethodName, System.StringComparison.Ordinal))
        {
            return false;
        }

        static INamedTypeSymbol? FindBootstrapDeclaringType(IMethodSymbol m)
        {
            var decl = m.ReducedFrom ?? m;
            return decl.ContainingType?.OriginalDefinition as INamedTypeSymbol;
        }

        if (FindBootstrapDeclaringType(methodSymbol) is not { } declaring
            || !SymbolEqualityComparer.Default.Equals(declaring, bootstrapType.OriginalDefinition))
        {
            return false;
        }

        // Reduced extension inference: the receiver type is the generation target. (A bound
        // method's TypeArguments would be the method's own type parameter, not the receiver.)
        if (invocation.Expression is MemberAccessExpressionSyntax { Expression: ExpressionSyntax receiver })
        {
            if (semanticModel.GetTypeInfo(receiver).Type is INamedTypeSymbol receiverNamed)
            {
                namedType = receiverNamed;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetBootstrapGenericConstraintTarget(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        IMethodSymbol methodSymbol,
        INamedTypeSymbol bootstrapType,
        string entryMethodName,
        out GenericConstraintTarget target)
    {
        target = default;
        if (!string.Equals(methodSymbol.Name, entryMethodName, System.StringComparison.Ordinal))
        {
            return false;
        }

        var declaration = methodSymbol.ReducedFrom ?? methodSymbol;
        if (declaration.ContainingType?.OriginalDefinition is not { } declaring
            || !SymbolEqualityComparer.Default.Equals(declaring, bootstrapType.OriginalDefinition))
        {
            return false;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax { Expression: ExpressionSyntax receiver })
        {
            return false;
        }

        if (semanticModel.GetTypeInfo(receiver).Type is not ITypeParameterSymbol typeParameter)
        {
            return false;
        }

        return TryCreateGenericConstraintTarget(typeParameter, out target);
    }

    private static bool TryCreateGenericConstraintTarget(
        ITypeParameterSymbol typeParameter,
        out GenericConstraintTarget target)
    {
        target = default;
        var constraintTypes = typeParameter.ConstraintTypes
            .OfType<INamedTypeSymbol>()
            .Where(static t => t.TypeKind is TypeKind.Class or TypeKind.Interface)
            .Where(static t => !ContainsTypeParameter(t))
            .OrderBy(static t => t.TypeKind == TypeKind.Class ? 0 : 1)
            .ThenBy(static t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), System.StringComparer.Ordinal)
            .ToImmutableArray();

        if (constraintTypes.IsDefaultOrEmpty)
        {
            return false;
        }

        if (!constraintTypes.SelectMany(static t => GetPublicInstanceEventsFromTypeAndBases(t)).Any())
        {
            return false;
        }

        target = new GenericConstraintTarget(constraintTypes);
        return true;
    }

    private static bool ContainsTypeParameter(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.TypeParameter)
        {
            return true;
        }

        return type switch
        {
            INamedTypeSymbol named => named.TypeArguments.Any(static t => ContainsTypeParameter(t)),
            IArrayTypeSymbol array => ContainsTypeParameter(array.ElementType),
            IPointerTypeSymbol pointer => ContainsTypeParameter(pointer.PointedAtType),
            _ => false,
        };
    }

    /// <summary>
    /// Public instance events declared on <paramref name="type"/> and its non-generic class base types (excluding <see cref="object"/>),
    /// with derived declarations taking precedence over the same event name on a base type.
    /// When <paramref name="type"/> is an interface, events declared on the interface and all its base interfaces are collected.
    /// </summary>
    private static IEnumerable<IEventSymbol> GetPublicInstanceEventsFromTypeAndBases(INamedTypeSymbol type)
    {
        var byName = new Dictionary<string, IEventSymbol>(System.StringComparer.Ordinal);

        if (type.TypeKind == TypeKind.Interface)
        {
            // Collect events from the interface itself and all base interfaces.
            foreach (var iface in new[] { type }.Concat(type.AllInterfaces))
            {
                foreach (var evt in iface.GetMembers().OfType<IEventSymbol>()
                             .Where(static e => !e.IsStatic))
                {
                    if (!byName.ContainsKey(evt.Name))
                    {
                        byName[evt.Name] = evt;
                    }
                }
            }
        }
        else
        {
            for (var current = type; current is not null; current = current.BaseType)
            {
                if (current.SpecialType == SpecialType.System_Object)
                {
                    break;
                }

                if (current.TypeKind != TypeKind.Class)
                {
                    continue;
                }

                foreach (var evt in current.GetMembers().OfType<IEventSymbol>()
                             .Where(static e => e is { IsStatic: false, DeclaredAccessibility: Accessibility.Public }))
                {
                    if (!byName.ContainsKey(evt.Name))
                    {
                        byName[evt.Name] = evt;
                    }
                }
            }
        }

        return byName.Values.OrderBy(static e => e.Name, System.StringComparer.Ordinal);
    }

}
