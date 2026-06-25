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
    ObservableEventsEntryKind entryKind)
{
    var receiverType = SyntaxFactory.ParseTypeName(ObservableEventsConstants.QualifiedType(target.ReceiverType));
    var observableMethod = entryKind == ObservableEventsEntryKind.AttachedRoutedEvent
        ? ObservableEventsConstants.AttachedRoutedEventEntryMethodName
        : ObservableEventsConstants.AttachedRoutedEventHandlerEntryMethodName;
    var returnType = entryKind == ObservableEventsEntryKind.AttachedRoutedEvent
        ? SyntaxFactory.ParseTypeName(
#if EVENTS_R3
            "global::R3.Observable<TEventArgs>"
#else
            "global::System.IObservable<TEventArgs>"
#endif
            )
        : SyntaxFactory.ParseTypeName(
#if EVENTS_R3
            "global::R3.Observable<(object? sender, TEventArgs e)>"
#else
            "global::System.IObservable<(object? sender, TEventArgs e)>"
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
                            SyntaxFactory.IdentifierName("TEventArgs"))))),
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
                            SyntaxFactory.IdentifierName("TEventArgs"))))),
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
            SyntaxFactory.ParseTypeName("global::System.EventHandler<TEventArgs>"),
            SyntaxFactory.IdentifierName("TEventArgs"),
            ObservableEventsSyntaxFactory.EventHandlerFactorySenderAndArgs(),
            subscribeHandler,
            unsubscribeHandler)
        : ObservableEventsSyntaxFactory.FromEventHandlerInvocation(
            SyntaxFactory.IdentifierName("TEventArgs"),
            subscribeHandler,
            unsubscribeHandler);

    var method = ObservableEventsSyntaxFactory.CreateAttachedRoutedEventMethod(
        observableMethod,
        returnType,
        receiverType,
        body);

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
