using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Observables.RoutedEvents.Reactive.SourceGenerators;

/// <summary>
/// Roslyn syntax helpers for <see cref="ObservableEventsGenerator"/> (System.Reactive / IObservable).
/// </summary>
internal static class ObservableEventsSyntaxFactory
{
    private static readonly NameSyntax LinqObservableName = ParseName("global::System.Reactive.Linq.Observable");

    public static TypeSyntax IoObservableType(TypeSyntax elementType) =>
        QualifiedName(
            ParseName("global::System"),
            GenericName(Identifier("IObservable"))
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(elementType))));

    public static TypeSyntax UnitIoObservableType() =>
        IoObservableType(ParseTypeName("global::System.Reactive.Unit"));

    public static TypeSyntax ObservableSenderArgsTupleType(TypeSyntax eventArgsType) =>
        IoObservableType(
            TupleType(
                SeparatedList(
                [
                    TupleElement(NullableType(PredefinedType(Token(SyntaxKind.ObjectKeyword))), Identifier("sender")),
                    TupleElement(eventArgsType, Identifier("e")),
                ])));

    public static TypeSyntax SystemEventHandlerType(TypeSyntax eventArgsType) =>
        QualifiedName(
            ParseName("global::System"),
            GenericName(Identifier("EventHandler"))
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(eventArgsType))));

    public static TypeSyntax NamedGenericType(string typeName, params string[] typeParameterNames)
    {
        if (typeParameterNames.Length == 0)
        {
            return ParseTypeName(typeName);
        }

        return GenericName(Identifier(typeName))
            .WithTypeArgumentList(
                TypeArgumentList(
                    SeparatedList(typeParameterNames.Select(static n => (TypeSyntax)IdentifierName(n)))));
    }

    public static TypeSyntax GetObservableReturnTypeSyntax(ImmutableArray<IParameterSymbol> parameters)
    {
        if (parameters.Length == 0)
        {
            return UnitIoObservableType();
        }

        if (parameters.Length == 1)
        {
            return IoObservableType(ParseTypeName(ObservableEventsConstants.QualifiedType(parameters[0].Type)));
        }

        if (parameters.Length == 2 && parameters[0].Type.SpecialType == SpecialType.System_Object)
        {
            return IoObservableType(ParseTypeName(ObservableEventsConstants.QualifiedType(parameters[1].Type)));
        }

        var tupleElements = parameters
            .Select(static p => TupleElement(ParseTypeName(ObservableEventsConstants.QualifiedType(p.Type))))
            .ToArray();
        return IoObservableType(TupleType(SeparatedList(tupleElements)));
    }

    public static TypeSyntax GetFromEventHandlersSenderReceiverReturnTypeSyntax(ImmutableArray<IParameterSymbol> parameters) =>
        IoObservableType(
            TupleType(
                SeparatedList(
                [
                    TupleElement(
                        ParseTypeName(ObservableEventsConstants.QualifiedType(parameters[0].Type)),
                        Identifier("sender")),
                    TupleElement(
                        ParseTypeName(ObservableEventsConstants.QualifiedType(parameters[1].Type)),
                        Identifier("e")),
                ])));

    public static SyntaxTriviaList CreateEventInheritDocTrivia(string cref) =>
        ParseLeadingTrivia(
            $"/// <summary>\n/// <inheritdoc cref=\"{cref}\" />\n/// </summary>\n");

    private static ExpressionSyntax UnitDefaultExpression() =>
        MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            ParseName("global::System.Reactive.Unit"),
            IdentifierName("Default"));

    private static ExpressionSyntax FromEventHandlerFactoryZeroArgs() =>
        SimpleLambdaExpression(
            Parameter(Identifier("h")),
            ParenthesizedLambdaExpression(
                ParameterList(SeparatedList<ParameterSyntax>()),
                InvocationExpression(
                    IdentifierName("h"),
                    ArgumentList(SingletonSeparatedList(Argument(UnitDefaultExpression()))))));

    private static ExpressionSyntax FromEventHandlerFactoryOneArg(string argName = "arg1") =>
        SimpleLambdaExpression(
            Parameter(Identifier("h")),
            SimpleLambdaExpression(
                Parameter(Identifier(argName)),
                InvocationExpression(
                    IdentifierName("h"),
                    ArgumentList(SingletonSeparatedList(Argument(IdentifierName(argName)))))));

    public static ExpressionSyntax FromEventHandlerFactorySenderAndArgs() =>
        SimpleLambdaExpression(
            Parameter(Identifier("h")),
            ParenthesizedLambdaExpression(
                ParameterList(
                    SeparatedList<ParameterSyntax>(
                    [
                        Parameter(Identifier("sender")),
                        Parameter(Identifier("e")),
                    ])),
                InvocationExpression(
                    IdentifierName("h"),
                    ArgumentList(SingletonSeparatedList(Argument(IdentifierName("e")))))));

    private static ExpressionSyntax FromEventHandlerFactoryTuple(ImmutableArray<IParameterSymbol> parameters)
    {
        var argNames = parameters.Select(static (_, i) => $"arg{i + 1}").ToArray();
        var lambdaParams = SeparatedList(argNames.Select(static n => Parameter(Identifier(n))));
        var tupleArgs = SeparatedList(argNames.Select(static n => Argument(IdentifierName(n))));
        return SimpleLambdaExpression(
            Parameter(Identifier("h")),
            ParenthesizedLambdaExpression(
                ParameterList(lambdaParams),
                InvocationExpression(
                    IdentifierName("h"),
                    ArgumentList(SingletonSeparatedList(Argument(TupleExpression(tupleArgs)))))));
    }

    private static ExpressionSyntax FromEventHandlerFactoryLegacyTuple() =>
        SimpleLambdaExpression(
            Parameter(Identifier("h")),
            ParenthesizedLambdaExpression(
                ParameterList(
                    SeparatedList<ParameterSyntax>(
                    [
                        Parameter(Identifier("sender")),
                        Parameter(Identifier("e")),
                    ])),
                InvocationExpression(
                    IdentifierName("h"),
                    ArgumentList(
                        SingletonSeparatedList(
                            Argument(
                                TupleExpression(
                                    SeparatedList<ArgumentSyntax>(
                                    [
                                        Argument(IdentifierName("sender")),
                                        Argument(IdentifierName("e")),
                                    ]))))))));

    public static StatementSyntax SenderAssignmentStatement() =>
        ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName("_sender"),
                IdentifierName("sender")));

    public static ClassDeclarationSyntax BootstrapExtensionsClassDeclaration() =>
        ClassDeclaration("ObservableEventsBootstrapExtensions")
            .AddModifiers(
                Token(SyntaxKind.InternalKeyword),
                Token(SyntaxKind.StaticKeyword),
                Token(SyntaxKind.PartialKeyword));

    public static MethodDeclarationSyntax CreateFromSenderExtensionMethod(
        string methodName,
        TypeSyntax returnType,
        TypeSyntax receiverType,
        TypeSyntax implementationType,
        TypeParameterListSyntax? typeParameters = null,
        SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses = default,
        SeparatedSyntaxList<ArgumentSyntax>? objectCreationArguments = null)
    {
        var ctorArgs = objectCreationArguments ?? SingletonSeparatedList(Argument(IdentifierName("source")));
        var method = MethodDeclaration(returnType, Identifier(methodName))
            .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
            .AddParameterListParameters(
                Parameter(Identifier("source"))
                    .WithType(receiverType)
                    .AddModifiers(Token(SyntaxKind.ThisKeyword)))
            .WithExpressionBody(
                ArrowExpressionClause(
                    ObjectCreationExpression(implementationType)
                        .WithArgumentList(ArgumentList(ctorArgs))))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        if (typeParameters is not null)
        {
            method = method.WithTypeParameterList(typeParameters);
        }

        if (constraintClauses.Count > 0)
        {
            method = method.WithConstraintClauses(constraintClauses);
        }

        return method;
    }

    public static ExpressionSyntax SenderMemberAccess(string eventName) =>
        MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            IdentifierName("_sender"),
            IdentifierName(eventName));

    public static ExpressionSyntax CastSenderMemberAccess(TypeSyntax castType, string eventName) =>
        MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            ParenthesizedExpression(
                CastExpression(castType, IdentifierName("_sender"))),
            IdentifierName(eventName));

    public static ExpressionSyntax EventSubscriptionAdd(ExpressionSyntax target, string handlerParameter = "e") =>
        SimpleLambdaExpression(
            Parameter(Identifier(handlerParameter)),
            AssignmentExpression(
                SyntaxKind.AddAssignmentExpression,
                target,
                IdentifierName(handlerParameter)));

    public static ExpressionSyntax EventSubscriptionRemove(ExpressionSyntax target, string handlerParameter = "e") =>
        SimpleLambdaExpression(
            Parameter(Identifier(handlerParameter)),
            AssignmentExpression(
                SyntaxKind.SubtractAssignmentExpression,
                target,
                IdentifierName(handlerParameter)));

    public static ExpressionSyntax HandlerSubscriptionLambda(
        ExpressionSyntax subscriptionExpression,
        string handlerParameter = "h") =>
        SimpleLambdaExpression(
            Parameter(Identifier(handlerParameter)),
            subscriptionExpression);

    public static InvocationExpressionSyntax ObservableFromEventInvocation(
        TypeSyntax delegateType,
        TypeSyntax elementType,
        ExpressionSyntax handlerLambda,
        ExpressionSyntax addAssignment,
        ExpressionSyntax removeAssignment) =>
        InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                LinqObservableName,
                GenericName(Identifier("FromEvent"))
                    .WithTypeArgumentList(
                        TypeArgumentList(SeparatedList<TypeSyntax>([delegateType, elementType])))),
            ArgumentList(
                SeparatedList(
                [
                    Argument(handlerLambda),
                    Argument(addAssignment),
                    Argument(removeAssignment),
                ])));

    public static InvocationExpressionSyntax ObservableFromEventHandlerInvocation(
        TypeSyntax? eventArgsType,
        ExpressionSyntax addExpression,
        ExpressionSyntax removeExpression)
    {
        var argsType = eventArgsType ?? ParseTypeName("global::System.EventArgs");
        var tupleType = TupleType(
            SeparatedList(
            [
                TupleElement(NullableType(PredefinedType(Token(SyntaxKind.ObjectKeyword))), Identifier("sender")),
                TupleElement(argsType, Identifier("e")),
            ]));
        var eventHandlerType = eventArgsType is null
            ? ParseTypeName("global::System.EventHandler")
            : SystemEventHandlerType(argsType);

        var conversion = SimpleLambdaExpression(
            Parameter(Identifier("h")),
            ParenthesizedLambdaExpression(
                ParameterList(
                    SeparatedList<ParameterSyntax>(
                    [
                        Parameter(Identifier("sender")),
                        Parameter(Identifier("e")),
                    ])),
                InvocationExpression(
                    IdentifierName("h"),
                    ArgumentList(
                        SingletonSeparatedList(
                            Argument(
                                TupleExpression(
                                    SeparatedList<ArgumentSyntax>(
                                    [
                                        Argument(IdentifierName("sender")),
                                        Argument(IdentifierName("e")),
                                    ]))))))));

        return ObservableFromEventInvocation(eventHandlerType, tupleType, conversion, addExpression, removeExpression);
    }

    public static ExpressionSyntax BuildFromEventObservableExpression(
        INamedTypeSymbol delegateType,
        ImmutableArray<IParameterSymbol> parameters,
        ExpressionSyntax eventAccessor)
    {
        var delegateTypeSyntax = ParseTypeName(ObservableEventsConstants.QualifiedType(delegateType));
        var add = EventSubscriptionAdd(eventAccessor);
        var remove = EventSubscriptionRemove(eventAccessor);

        if (parameters.Length == 0)
        {
            return ObservableFromEventInvocation(
                delegateTypeSyntax,
                ParseTypeName("global::System.Reactive.Unit"),
                FromEventHandlerFactoryZeroArgs(),
                add,
                remove);
        }

        if (parameters.Length == 1)
        {
            var elementType = ParseTypeName(ObservableEventsConstants.QualifiedType(parameters[0].Type));
            return ObservableFromEventInvocation(
                delegateTypeSyntax,
                elementType,
                FromEventHandlerFactoryOneArg(),
                add,
                remove);
        }

        if (parameters.Length == 2 && parameters[0].Type.SpecialType == SpecialType.System_Object)
        {
            var elementType = ParseTypeName(ObservableEventsConstants.QualifiedType(parameters[1].Type));
            return ObservableFromEventInvocation(
                delegateTypeSyntax,
                elementType,
                FromEventHandlerFactorySenderAndArgs(),
                add,
                remove);
        }

        var tupleTypes = parameters.Select(static p => ParseTypeName(ObservableEventsConstants.QualifiedType(p.Type)));
        var tupleType = TupleType(SeparatedList(tupleTypes.Select(static t => TupleElement(t))));
        return ObservableFromEventInvocation(
            delegateTypeSyntax,
            tupleType,
            FromEventHandlerFactoryTuple(parameters),
            add,
            remove);
    }

    public static ExpressionSyntax BuildLegacySenderReceiverFromEventExpression(
        INamedTypeSymbol delegateType,
        ImmutableArray<IParameterSymbol> parameters,
        ExpressionSyntax eventAccessor)
    {
        var delegateTypeSyntax = ParseTypeName(ObservableEventsConstants.QualifiedType(delegateType));
        var p0 = ParseTypeName(ObservableEventsConstants.QualifiedType(parameters[0].Type));
        var p1 = ParseTypeName(ObservableEventsConstants.QualifiedType(parameters[1].Type));
        var tupleType = TupleType(SeparatedList([TupleElement(p0), TupleElement(p1)]));
        return ObservableFromEventInvocation(
            delegateTypeSyntax,
            tupleType,
            FromEventHandlerFactoryLegacyTuple(),
            EventSubscriptionAdd(eventAccessor),
            EventSubscriptionRemove(eventAccessor));
    }

    public static SeparatedSyntaxList<ArgumentSyntax> AvaloniaRoutedImplConstructorArguments()
    {
        var routingStrategies = ParseName("global::Avalonia.Interactivity.RoutingStrategies");
        var routesDefault = BinaryExpression(
            SyntaxKind.BitwiseOrExpression,
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, routingStrategies, IdentifierName("Direct")),
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, routingStrategies, IdentifierName("Bubble")));

        return SeparatedList(
        [
            Argument(IdentifierName("source")),
            Argument(routesDefault),
            Argument(LiteralExpression(SyntaxKind.FalseLiteralExpression, Token(SyntaxKind.FalseKeyword))),
        ]);
    }

    public static MethodDeclarationSyntax CreateAvaloniaRoutedExtensionMethod(
        string methodName,
        TypeSyntax returnType,
        TypeSyntax receiverType,
        TypeSyntax implementationType)
    {
        return MethodDeclaration(returnType, Identifier(methodName))
            .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
            .AddParameterListParameters(
                Parameter(Identifier("source"))
                    .WithType(receiverType)
                    .AddModifiers(Token(SyntaxKind.ThisKeyword)),
                Parameter(Identifier("routes"))
                    .WithType(ParseTypeName("global::Avalonia.Interactivity.RoutingStrategies")),
                Parameter(Identifier("handledEventsToo"))
                    .WithType(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                    .WithDefault(
                        EqualsValueClause(LiteralExpression(SyntaxKind.FalseLiteralExpression, Token(SyntaxKind.FalseKeyword)))))
            .WithExpressionBody(
                ArrowExpressionClause(
                    ObjectCreationExpression(implementationType)
                        .WithArgumentList(
                            ArgumentList(
                                SeparatedList(
                                [
                                    Argument(IdentifierName("source")),
                                    Argument(IdentifierName("routes")),
                                    Argument(IdentifierName("handledEventsToo")),
                                ])))))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }

    public static MethodDeclarationSyntax CreateAttachedRoutedEventMethod(
        string methodName,
        TypeSyntax returnType,
        TypeSyntax receiverType,
        ExpressionSyntax bodyExpression)
    {
        var routedEventType = ParseTypeName("global::Avalonia.Interactivity.RoutedEvent<TEventArgs>");
        var routesType = ParseTypeName("global::Avalonia.Interactivity.RoutingStrategies");
        var routingStrategies = ParseName("global::Avalonia.Interactivity.RoutingStrategies");
        var routesDefault = BinaryExpression(
            SyntaxKind.BitwiseOrExpression,
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, routingStrategies, IdentifierName("Direct")),
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, routingStrategies, IdentifierName("Bubble")));

        return MethodDeclaration(returnType, Identifier(methodName))
            .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
            .WithTypeParameterList(TypeParameterList(SingletonSeparatedList(TypeParameter("TEventArgs"))))
            .WithConstraintClauses(
                SingletonList(
                    TypeParameterConstraintClause("TEventArgs")
                        .WithConstraints(
                            SingletonSeparatedList<TypeParameterConstraintSyntax>(
                                TypeConstraint(ParseTypeName("global::Avalonia.Interactivity.RoutedEventArgs"))))))
            .AddParameterListParameters(
                Parameter(Identifier("source"))
                    .WithType(receiverType)
                    .AddModifiers(Token(SyntaxKind.ThisKeyword)),
                Parameter(Identifier("routedEvent"))
                    .WithType(routedEventType),
                Parameter(Identifier("routes"))
                    .WithType(routesType)
                    .WithDefault(EqualsValueClause(routesDefault)),
                Parameter(Identifier("handledEventsToo"))
                    .WithType(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                    .WithDefault(
                        EqualsValueClause(LiteralExpression(SyntaxKind.FalseLiteralExpression, Token(SyntaxKind.FalseKeyword)))))
            .WithExpressionBody(ArrowExpressionClause(bodyExpression))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }

    public static PropertyDeclarationSyntax CreateAvaloniaRoutedEventProperty(
        IEventSymbol evt,
        IFieldSymbol routedEventField,
        INamedTypeSymbol eventArgsType,
        bool useEventHandlers,
        SyntaxTriviaList? documentation = null)
    {
        var eventArgs = ParseTypeName(ObservableEventsConstants.QualifiedType(eventArgsType));
        var eventFieldAccess = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            ParseName(ObservableEventsConstants.QualifiedType(routedEventField.ContainingType)),
            IdentifierName(routedEventField.Name));

        var addHandler = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("_sender"),
                GenericName(Identifier("AddHandler"))
                    .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(eventArgs)))),
            ArgumentList(
                SeparatedList(
                [
                    Argument(eventFieldAccess),
                    Argument(IdentifierName("h")),
                    Argument(IdentifierName("_routes")),
                    Argument(IdentifierName("_handledEventsToo")),
                ])));

        var removeHandler = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("_sender"),
                GenericName(Identifier("RemoveHandler"))
                    .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(eventArgs)))),
            ArgumentList(
                SeparatedList(
                [
                    Argument(eventFieldAccess),
                    Argument(IdentifierName("h")),
                ])));

        var subscribeHandler = HandlerSubscriptionLambda(addHandler);
        var unsubscribeHandler = HandlerSubscriptionLambda(removeHandler);
        ExpressionSyntax body;
        TypeSyntax returnType;
        if (useEventHandlers)
        {
            returnType = ObservableSenderArgsTupleType(eventArgs);
            body = ObservableFromEventHandlerInvocation(eventArgs, subscribeHandler, unsubscribeHandler);
        }
        else
        {
            returnType = IoObservableType(eventArgs);
            body = ObservableFromEventInvocation(
                SystemEventHandlerType(eventArgs),
                eventArgs,
                FromEventHandlerFactorySenderAndArgs(),
                subscribeHandler,
                unsubscribeHandler);
        }

        var property = PropertyDeclaration(returnType, Identifier(evt.Name))
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .WithExpressionBody(ArrowExpressionClause(body))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        if (documentation is { Count: > 0 })
        {
            property = property.WithLeadingTrivia(documentation.Value);
        }

        return property;
    }
}
