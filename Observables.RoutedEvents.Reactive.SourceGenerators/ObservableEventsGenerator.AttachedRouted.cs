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

namespace Observables.RoutedEvents.Reactive.SourceGenerators;

public sealed partial class ObservableEventsGenerator
{
private static string GenerateAttachedRoutedEventSourceForTarget(
    AttachedRoutedEventTarget target,
    ObservableEventsEntryKind entryKind)
{
    var receiverType = SyntaxFactory.ParseTypeName(ObservableEventsConstants.QualifiedType(target.ReceiverType));
    var observableMethod = entryKind == ObservableEventsEntryKind.FromAttachedRoutedEvent
        ? ObservableEventsConstants.FromAttachedRoutedEventEntryMethodName
        : ObservableEventsConstants.FromAttachedRoutedEventHandlerEntryMethodName;
    var returnType = entryKind == ObservableEventsEntryKind.FromAttachedRoutedEvent
        ? ObservableEventsSyntaxFactory.IoObservableType(SyntaxFactory.IdentifierName("TEventArgs"))
        : ObservableEventsSyntaxFactory.ObservableSenderArgsTupleType(SyntaxFactory.IdentifierName("TEventArgs"));

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
    ExpressionSyntax body = entryKind == ObservableEventsEntryKind.FromAttachedRoutedEvent
        ? ObservableEventsSyntaxFactory.ObservableFromEventInvocation(
            SyntaxFactory.ParseTypeName("global::System.EventHandler<TEventArgs>"),
            SyntaxFactory.IdentifierName("TEventArgs"),
            ObservableEventsSyntaxFactory.FromEventHandlerFactorySenderAndArgs(),
            subscribeHandler,
            unsubscribeHandler)
        : ObservableEventsSyntaxFactory.ObservableFromEventHandlerInvocation(
            SyntaxFactory.IdentifierName("TEventArgs"),
            subscribeHandler,
            unsubscribeHandler);

    var method = ObservableEventsSyntaxFactory.CreateAttachedRoutedEventMethod(
        observableMethod,
        returnType,
        receiverType,
        body);

    var unit = SyntaxFactory.CompilationUnit()
        .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Reactive.Linq")))
        .AddMembers(
            SyntaxFactory.FileScopedNamespaceDeclaration(SyntaxFactory.ParseName(ObservableEventsConstants.GeneratedNamespace))
                .AddMembers(
                    ObservableEventsSyntaxFactory.BootstrapExtensionsClassDeclaration()
                        .AddMembers(method)));

    return GeneratedSourceHeader.ToSource(unit);
}
}
