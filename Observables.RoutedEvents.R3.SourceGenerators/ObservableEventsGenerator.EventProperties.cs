using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Observables.SourceGenerators.Shared.Diagnostics;
using Observables.SourceGenerators.Shared.Extensions;

namespace Observables.RoutedEvents.R3.SourceGenerators;

public sealed partial class ObservableEventsGenerator
{
private static bool TryCreateEventObservableProperty(
    IEventSymbol evt,
    ExpressionSyntax eventAccessorExpression,
    SourceProductionContext context,
    out PropertyDeclarationSyntax property,
    bool includeXmlDocumentation = true)
{
    property = null!;
    if (evt.Type is not INamedTypeSymbol delegateType || delegateType.DelegateInvokeMethod is not IMethodSymbol invoke)
    {
        ReportInvalidDelegate(evt, context);
        return false;
    }

    if (!invoke.ReturnsVoid)
    {
        ReportInvalidDelegate(evt, context);
        return false;
    }

    var returnType = ObservableEventsSyntaxFactory.GetObservableReturnTypeSyntax(invoke.Parameters);
    var bodyExpression = ObservableEventsSyntaxFactory.BuildEventObservableExpression(
        delegateType,
        invoke.Parameters,
        eventAccessorExpression);
    property = SyntaxFactory.PropertyDeclaration(returnType, evt.Name)
        .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
        .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(bodyExpression))
        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
    if (includeXmlDocumentation)
    {
        property = property.WithLeadingTrivia(ObservableEventsSyntaxFactory.CreateEventInheritDocTrivia(
            $"{ObservableEventsConstants.QualifiedType(evt.ContainingType)}.{evt.Name}"));
    }

    return true;
}

private static bool TryCreateEventHandlerObservableProperty(
    IEventSymbol evt,
    ExpressionSyntax eventAccessorExpression,
    Compilation compilation,
    SourceProductionContext context,
    out PropertyDeclarationSyntax property,
    bool includeXmlDocumentation = true)
{
    property = null!;
    if (evt.Type is not INamedTypeSymbol delegateType || delegateType.DelegateInvokeMethod is not IMethodSymbol invoke)
    {
        ReportInvalidEventHandlersDelegate(evt, context);
        return false;
    }

    if (!invoke.ReturnsVoid)
    {
        ReportInvalidEventHandlersDelegate(evt, context);
        return false;
    }

    TypeSyntax returnType;
    ExpressionSyntax bodyExpression;

    if (IsClassicSystemEventHandler(delegateType, compilation, out var genericEventArgs))
    {
        var add = ObservableEventsSyntaxFactory.EventSubscriptionAdd(eventAccessorExpression);
        var remove = ObservableEventsSyntaxFactory.EventSubscriptionRemove(eventAccessorExpression);
        var eventArgsType = genericEventArgs is null
            ? SyntaxFactory.ParseTypeName("global::System.EventArgs")
            : SyntaxFactory.ParseTypeName(ObservableEventsConstants.QualifiedType(genericEventArgs));
        bodyExpression = ObservableEventsSyntaxFactory.RxFromEventHandlerInvocation(
            genericEventArgs is null ? null : eventArgsType,
            add,
            remove);
        returnType = ObservableEventsSyntaxFactory.R3ObservableSenderArgsTupleType(eventArgsType);
    }
    else if (IsLegacySenderReceiverDelegate(delegateType, invoke, compilation))
    {
        bodyExpression = ObservableEventsSyntaxFactory.BuildLegacySenderReceiverEventExpression(
            delegateType,
            invoke.Parameters,
            eventAccessorExpression);
        returnType = ObservableEventsSyntaxFactory.GetEventHandlersSenderReceiverReturnTypeSyntax(invoke.Parameters);
    }
    else
    {
        ReportInvalidEventHandlersDelegate(evt, context);
        return false;
    }

    property = SyntaxFactory.PropertyDeclaration(returnType, evt.Name)
        .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
        .WithExpressionBody(
            SyntaxFactory.ArrowExpressionClause(bodyExpression))
        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
    if (includeXmlDocumentation)
    {
        property = property.WithLeadingTrivia(ObservableEventsSyntaxFactory.CreateEventInheritDocTrivia(
            $"{ObservableEventsConstants.QualifiedType(evt.ContainingType)}.{evt.Name}"));
    }

    return true;
}

/// <summary>
/// Custom <c>void (object, TSecond)</c> delegate excluding <c>System.EventHandler</c> / <c>System.EventHandler&lt;T&gt;</c> (those use <c>Observable.FromEventHandler</c>), implemented with <c>R3.Observable.FromEvent</c>.
/// </summary>
private static bool IsLegacySenderReceiverDelegate(INamedTypeSymbol delegateType, IMethodSymbol invoke, Compilation compilation)
{
    if (invoke.Parameters.Length != 2)
    {
        return false;
    }

    if (invoke.Parameters[0].RefKind != RefKind.None || invoke.Parameters[1].RefKind != RefKind.None)
    {
        return false;
    }

    if (!IsDeclaredObject(invoke.Parameters[0].Type, compilation))
    {
        return false;
    }

    if (IsClassicSystemEventHandler(delegateType, compilation, out _))
    {
        return false;
    }

    return true;
}

private static bool IsDeclaredObject(ITypeSymbol type, Compilation compilation)
{
    return SymbolEqualityComparer.Default.Equals(
        type.WithNullableAnnotation(NullableAnnotation.None),
        compilation.GetSpecialType(SpecialType.System_Object));
}

/// <returns><see langword="null"/> for non-generic <c>System.EventHandler</c>; otherwise the generic event-args type.</returns>
private static bool IsClassicSystemEventHandler(INamedTypeSymbol delegateType, Compilation compilation, out INamedTypeSymbol? genericEventArgs)
{
    genericEventArgs = null;
    var nonGeneric = compilation.GetTypeByMetadataName("System.EventHandler");
    var genericDef = compilation.GetTypeByMetadataName("System.EventHandler`1");
    if (nonGeneric is null || genericDef is null)
    {
        return false;
    }

    if (SymbolEqualityComparer.Default.Equals(delegateType.OriginalDefinition, nonGeneric))
    {
        return true;
    }

    if (SymbolEqualityComparer.Default.Equals(delegateType.OriginalDefinition, genericDef)
        && delegateType.TypeArguments.Length == 1
        && delegateType.TypeArguments[0] is INamedTypeSymbol tArg)
    {
        genericEventArgs = tArg;
        return true;
    }

    return false;
}

}
