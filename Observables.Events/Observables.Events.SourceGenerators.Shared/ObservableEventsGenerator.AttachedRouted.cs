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
    private static string GenerateAttachedRoutedEventSourceForTarget(
        AttachedRoutedEventTarget target,
        ObservableEventsEntryKind entryKind,
        Compilation compilation)
    {
        // The generated extension must be able to name the receiver type.
        if (!compilation.IsSymbolAccessibleWithin(target.ReceiverType, compilation.Assembly))
            return string.Empty;

        var receiverType = SyntaxFactory.ParseTypeName(ObservableEventsConstants.QualifiedType(target.ReceiverType));

        // Discovery normalizes receivers to their OriginalDefinition — redeclare the receiver's
        // own type parameters on the generated method, before our event-args parameter.
        TypeParameterListSyntax? receiverTypeParameters = null;
        if (target.ReceiverType.IsGenericType)
        {
            receiverTypeParameters = SyntaxFactory.TypeParameterList(
                SyntaxFactory.SeparatedList(
                    target.ReceiverType.TypeParameters.Select(static tp => SyntaxFactory.TypeParameter(tp.Name))));
        }

        var receiverConstraints = CreateSourceTypeConstraintClauses(target.ReceiverType);
        var eventArgsName = target.ReceiverType.TypeParameters.Any(static tp => tp.Name == "TEventArgs")
            ? "TArgs"
            : "TEventArgs";

        var observableMethod = entryKind == ObservableEventsEntryKind.AttachedRoutedEvent
            ? ObservableEventsConstants.AttachedRoutedEventEntryMethodName
            : ObservableEventsConstants.AttachedRoutedEventHandlerEntryMethodName;
        var returnType = entryKind == ObservableEventsEntryKind.AttachedRoutedEvent
            ? SyntaxFactory.ParseTypeName(
#if EVENTS_R3
                $"global::R3.Observable<{eventArgsName}>"
#else
            $"global::System.IObservable<{eventArgsName}>"
#endif
                )
            : SyntaxFactory.ParseTypeName(
#if EVENTS_R3
                $"global::R3.Observable<(object? sender, {eventArgsName} e)>"
#else
            $"global::System.IObservable<(object? sender, {eventArgsName} e)>"
#endif
                );

        var routedEventParam = SyntaxFactory.IdentifierName("routedEvent");
        var addHandler = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("source"),
                SyntaxFactory.GenericName(SyntaxFactory.Identifier("AddHandler"))
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                SyntaxFactory.IdentifierName(eventArgsName))))),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(
                [
                    SyntaxFactory.Argument(routedEventParam),
                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("h")),
                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("routes")),
                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("handledEventsToo")),
                ])));

        var removeHandler = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("source"),
                SyntaxFactory.GenericName(SyntaxFactory.Identifier("RemoveHandler"))
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                SyntaxFactory.IdentifierName(eventArgsName))))),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(
                [
                    SyntaxFactory.Argument(routedEventParam),
                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("h")),
                ])));

        var subscribeHandler = ObservableEventsSyntaxFactory.HandlerSubscriptionLambda(addHandler);
        var unsubscribeHandler = ObservableEventsSyntaxFactory.HandlerSubscriptionLambda(removeHandler);
        ExpressionSyntax body = entryKind == ObservableEventsEntryKind.AttachedRoutedEvent
            ? ObservableEventsSyntaxFactory.FromEventInvocation(
                SyntaxFactory.ParseTypeName($"global::System.EventHandler<{eventArgsName}>"),
                SyntaxFactory.IdentifierName(eventArgsName),
                ObservableEventsSyntaxFactory.EventHandlerFactorySenderAndArgs(),
                subscribeHandler,
                unsubscribeHandler)
            : ObservableEventsSyntaxFactory.FromEventHandlerInvocation(
                SyntaxFactory.IdentifierName(eventArgsName),
                subscribeHandler,
                unsubscribeHandler);

        var method = ObservableEventsSyntaxFactory.CreateAttachedRoutedEventMethod(
            observableMethod,
            returnType,
            receiverType,
            body,
            receiverTypeParameters,
            receiverConstraints,
            eventArgsName);

        var unit = SyntaxFactory.CompilationUnit()
#if EVENTS_R3
            .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("R3")))
#endif
            .AddMembers(
                SyntaxFactory.FileScopedNamespaceDeclaration(SyntaxFactory.ParseName(ObservableEventsConstants.GeneratedNamespace))
                    .AddMembers(
                        ObservableEventsSyntaxFactory.BootstrapExtensionsClassDeclaration()
                            .AddMembers(method)));

        return GeneratedSourceHeader.ToSource(unit);
    }
}
