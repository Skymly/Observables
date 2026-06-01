using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Observables.Events.R3.SourceGenerators;

/// <summary>
/// Roslyn syntax helpers for <see cref="ObservableEventsGenerator"/> (avoids string-built source).
/// </summary>
internal static class ObservableEventsSyntaxFactory
{
    private static readonly NameSyntax R3Name = ParseName("global::R3");

    public static TypeSyntax R3ObservableType(TypeSyntax elementType) =>
        QualifiedName(
            R3Name,
            GenericName(Identifier("Observable"))
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(elementType))));

    public static TypeSyntax R3UnitObservableType() =>
        R3ObservableType(ParseTypeName("global::R3.Unit"));

    public static TypeSyntax R3ObservableSenderArgsTupleType(TypeSyntax eventArgsType) =>
        R3ObservableType(
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
            return R3UnitObservableType();
        }

        if (parameters.Length == 1)
        {
            return R3ObservableType(ParseTypeName(ObservableEventsConstants.QualifiedType(parameters[0].Type)));
        }

        if (parameters.Length == 2 && parameters[0].Type.SpecialType == SpecialType.System_Object)
        {
            return R3ObservableType(ParseTypeName(ObservableEventsConstants.QualifiedType(parameters[1].Type)));
        }

        var tupleElements = parameters
            .Select(static p => TupleElement(ParseTypeName(ObservableEventsConstants.QualifiedType(p.Type))))
            .ToArray();
        return R3ObservableType(TupleType(SeparatedList(tupleElements)));
    }

    public static TypeSyntax GetFromEventHandlersSenderReceiverReturnTypeSyntax(ImmutableArray<IParameterSymbol> parameters) =>
        R3ObservableType(
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

    private static ExpressionSyntax R3UnitDefaultExpression() =>
        MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            ParseName("global::R3.Unit"),
            IdentifierName("Default"));

    private static ExpressionSyntax FromEventHandlerFactoryZeroArgs() =>
        SimpleLambdaExpression(
            Parameter(Identifier("h")),
            ParenthesizedLambdaExpression(
                ParameterList(SeparatedList<ParameterSyntax>()),
                InvocationExpression(
                    IdentifierName("h"),
                    ArgumentList(SingletonSeparatedList(Argument(R3UnitDefaultExpression()))))));

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
                ParseName("global::R3.Observable"),
                GenericName(Identifier("FromEvent"))
                    .WithTypeArgumentList(
                        TypeArgumentList(SeparatedList<TypeSyntax>([delegateType, elementType])))),
            ArgumentList(
                SeparatedList(
                [
                    Argument(handlerLambda),
                    Argument(addAssignment),
                    Argument(removeAssignment),
                    Argument(LiteralExpression(SyntaxKind.DefaultLiteralExpression, Token(SyntaxKind.DefaultKeyword))),
                ])));

    public static InvocationExpressionSyntax ObservableFromEventHandlerInvocation(
        TypeSyntax? eventArgsType,
        ExpressionSyntax addExpression,
        ExpressionSyntax removeExpression)
    {
        SimpleNameSyntax fromEventHandler = eventArgsType is null
            ? IdentifierName("FromEventHandler")
            : GenericName(Identifier("FromEventHandler"))
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(eventArgsType)));

        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                ParseName("global::R3.Observable"),
                fromEventHandler),
            ArgumentList(
                SeparatedList(
                [
                    Argument(addExpression),
                    Argument(removeExpression),
                    Argument(LiteralExpression(SyntaxKind.DefaultLiteralExpression, Token(SyntaxKind.DefaultKeyword))),
                ])));
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
                ParseTypeName("global::R3.Unit"),
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
}
