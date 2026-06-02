using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Observables.SourceGenerators.Shared;
using Observables.SourceGenerators.Shared.Extensions;

namespace Observables.Events.R3.SourceGenerators;

public sealed partial class ObservableEventsGenerator
{
private static void RegisterObservableEventsStaticsShellPostInit(IncrementalGeneratorInitializationContext context)
{
#pragma warning disable CS0162 // Unreachable when ObservableEventsConstants.StaticObservableEventsGenerationEnabled is compile-time false
    if (!ObservableEventsConstants.StaticObservableEventsGenerationEnabled)
    {
        return;
    }

    context.RegisterPostInitializationOutput(static ctx =>
        ctx.AddSource(
            "Observables.Events.R3.ObservableEventsStatics.g.cs",
            SourceText.From(
                GeneratedSourceHeader.ToSource(
                    EventsBootstrapSyntaxFactory.CreateObservableEventsStaticsShellCompilationUnit()),
                Encoding.UTF8)));
#pragma warning restore CS0162
}

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
        or ObservableEventsConstants.EventHandlersEntryMethodName;
}

/// <summary>
/// Matches <c>ObservableEventsStatics.OBS_<em>StableHint</em>.Events</c> (static entry property), not <c>receiver.Events()</c>.
/// </summary>
private static bool IsStaticEventsEntryMemberAccess(SyntaxNode node)
{
    if (node is not MemberAccessExpressionSyntax ma)
    {
        return false;
    }

    if (!string.Equals(ma.Name.Identifier.ValueText, ObservableEventsConstants.EventsEntryMethodName, System.StringComparison.Ordinal))
    {
        return false;
    }

    // Exclude instance extension call shape: source.Events()
    if (ma.Parent is InvocationExpressionSyntax inv && ReferenceEquals(inv.Expression, ma))
    {
        return false;
    }

    if (ma.Expression is not MemberAccessExpressionSyntax obsAccess)
    {
        return false;
    }

    if (obsAccess.Expression is not IdentifierNameSyntax outerId
        || !string.Equals(outerId.Identifier.ValueText, "ObservableEventsStatics", System.StringComparison.Ordinal))
    {
        return false;
    }

    return obsAccess.Name switch
    {
        SimpleNameSyntax sn => sn.Identifier.ValueText.StartsWith("OBS_", System.StringComparison.Ordinal),
        _ => false,
    };
}

private static ObservableEventTargetSets CollectObservableEventTargets(
    Compilation compilation,
    ImmutableArray<SyntaxNode> candidates)
{
    var bootstrapType = compilation.GetTypeByMetadataName(ObservableEventsConstants.BootstrapExtensionsMetadataName);
    if (bootstrapType is null)
    {
        return ObservableEventTargetSets.Empty;
    }

    // Use pooled hash sets for better performance with large candidate sets
    var events = new System.Collections.Generic.HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
    var eventHandlers = new System.Collections.Generic.HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
    var eventsGenericConstraints = new Dictionary<string, GenericConstraintTarget>(System.StringComparer.Ordinal);
    var eventHandlersGenericConstraints = new Dictionary<string, GenericConstraintTarget>(System.StringComparer.Ordinal);

    foreach (var candidate in candidates)
    {
        if (candidate is InvocationExpressionSyntax invocation)
        {
            var semanticModel = compilation.GetSemanticModel(invocation.SyntaxTree);
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

        if (ObservableEventsConstants.StaticObservableEventsGenerationEnabled
            && candidate is MemberAccessExpressionSyntax staticEvents
            && IsStaticEventsEntryMemberAccess(staticEvents))
        {
            var semanticModel = compilation.GetSemanticModel(staticEvents.SyntaxTree);
            if (semanticModel.GetSymbolInfo(staticEvents).Symbol is { } staticSymbol
                && TryGetTypeFromObservableEventsStaticsNested(staticSymbol, bootstrapType, compilation, out var staticTarget))
            {
                if (staticTarget.IsGenericType)
                {
                    staticTarget = staticTarget.OriginalDefinition;
                }

                events.Add(staticTarget);
                continue;
            }

            // Cold compile: static entry property not bound until nested type exists.
            if (TryGetStaticObservableEventsTargetFromMemberAccess(compilation, staticEvents, out var syntaxOnlyStatic))
            {
                if (syntaxOnlyStatic.IsGenericType)
                {
                    syntaxOnlyStatic = syntaxOnlyStatic.OriginalDefinition;
                }

                events.Add(syntaxOnlyStatic);
            }
        }
    }

    static ImmutableArray<INamedTypeSymbol> Order(System.Collections.Generic.HashSet<INamedTypeSymbol> set) =>
        set
            .OrderBy(static t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), System.StringComparer.Ordinal)
            .ToImmutableArray();

    static ImmutableArray<GenericConstraintTarget> OrderGeneric(Dictionary<string, GenericConstraintTarget> set) =>
        set
            .OrderBy(static pair => pair.Key, System.StringComparer.Ordinal)
            .Select(static pair => pair.Value)
            .ToImmutableArray();

    return new ObservableEventTargetSets(
        Order(events),
        Order(eventHandlers),
        OrderGeneric(eventsGenericConstraints),
        OrderGeneric(eventHandlersGenericConstraints));
}

/// <summary>
/// When semantic binding cannot resolve the static entry property yet, recover the declaring type from
/// <c>ObservableEventsStatics.OBS_<em>StableHint</em>.Events</c> syntax so generation still runs.
/// </summary>
private static bool TryGetStaticObservableEventsTargetFromMemberAccess(
    Compilation compilation,
    MemberAccessExpressionSyntax eventsAccess,
    out INamedTypeSymbol namedType)
{
    namedType = null!;
    if (eventsAccess.Expression is not MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax { Identifier.ValueText: "ObservableEventsStatics" },
            Name: SimpleNameSyntax staticHintNameSyntax,
        })
    {
        return false;
    }

    if (!string.Equals(eventsAccess.Name.Identifier.ValueText, ObservableEventsConstants.EventsEntryMethodName, System.StringComparison.Ordinal))
    {
        return false;
    }

    var nestedId = staticHintNameSyntax.Identifier.ValueText;
    if (!nestedId.StartsWith("OBS_", System.StringComparison.Ordinal))
    {
        return false;
    }

    var hintStem = nestedId.Substring(4);
    return TryResolveNamedTypeByStableHint(compilation, hintStem, out namedType);
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

    if (methodSymbol.TypeArguments.Length == 1 && methodSymbol.TypeArguments[0] is INamedTypeSymbol fromArgs)
    {
        namedType = fromArgs;
        return true;
    }

    // Reduced extension inference: Events() on explicit receiver without TypeArguments surfaced on symbol.
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
/// Parses <c>ObservableEventsStatics.OBS_<em>StableHint</em>.Events</c> from semantic model (expects the static entry property on the nested partial class).
/// </summary>
private static bool TryGetTypeFromObservableEventsStaticsNested(
    ISymbol symbol,
    INamedTypeSymbol bootstrapType,
    Compilation compilation,
    out INamedTypeSymbol namedType)
{
    namedType = null!;

    if (symbol.Name != ObservableEventsConstants.EventsEntryMethodName)
    {
        return false;
    }

    if (symbol is IMethodSymbol methodSymbol)
    {
        if (methodSymbol.ReducedFrom is not null || methodSymbol.IsExtensionMethod)
        {
            return false;
        }
    }
    else if (symbol is not IPropertySymbol)
    {
        return false;
    }

    var nested = symbol.ContainingType;
    var outer = nested?.ContainingType;
    if (nested is null || outer is null)
    {
        return false;
    }

    if (!string.Equals(outer.Name, "ObservableEventsStatics", System.StringComparison.Ordinal))
    {
        return false;
    }

    if (!SymbolEqualityComparer.Default.Equals(outer.ContainingNamespace, bootstrapType.ContainingNamespace))
    {
        return false;
    }

    if (!nested.Name.StartsWith("OBS_", System.StringComparison.Ordinal))
    {
        return false;
    }

    var hintStem = nested.Name.Substring(4);
    return TryResolveNamedTypeByStableHint(compilation, hintStem, out namedType);
}

/// <remarks>
/// Must match identifiers produced via <see cref="INamedTypeSymbolExtensions.GetSafeHintName"/>.
/// </remarks>
private static bool TryResolveNamedTypeByStableHint(Compilation compilation, string hintStem, out INamedTypeSymbol namedType)
{
    namedType = null!;
    foreach (var candidate in EnumerateNamedTypesIncludingNested(compilation.GlobalNamespace))
    {
        if (!string.Equals(candidate.GetSafeHintName(), hintStem, System.StringComparison.Ordinal))
        {
            continue;
        }

        namedType = candidate;
        return true;
    }

    return false;
}

private static IEnumerable<INamedTypeSymbol> EnumerateNamedTypesIncludingNested(INamespaceSymbol root)
{
    foreach (var member in root.GetNamespaceMembers())
    {
        foreach (var t in EnumerateNamedTypesIncludingNested(member))
        {
            yield return t;
        }
    }

    foreach (var named in root.GetTypeMembers())
    {
        foreach (var t in EnumerateSelfAndNestedNamedTypes(named))
        {
            yield return t;
        }
    }
}

private static IEnumerable<INamedTypeSymbol> EnumerateSelfAndNestedNamedTypes(INamedTypeSymbol type)
{
    yield return type;
    foreach (var nested in type.GetTypeMembers())
    {
        foreach (var t in EnumerateSelfAndNestedNamedTypes(nested))
        {
            yield return t;
        }
    }
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
