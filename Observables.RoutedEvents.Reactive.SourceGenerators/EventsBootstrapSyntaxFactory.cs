using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Observables.RoutedEvents.Reactive.SourceGenerators;

/// <summary>Post-initialization bootstrap sources built with SyntaxFactory.</summary>
internal static class EventsBootstrapSyntaxFactory
{
    private const string BootstrapNamespace = "Observables.RoutedEvents.Reactive";
    private static readonly NameSyntax BootstrapNamespaceName = ParseName(BootstrapNamespace);
    private static readonly TypeSyntax NullEventsType = QualifiedName(BootstrapNamespaceName, IdentifierName("NullEvents"));

    public static CompilationUnitSyntax CreateNullEventsCompilationUnit() =>
        CompilationUnit()
            .AddMembers(
                NamespaceDeclaration(BootstrapNamespaceName)
                    .AddMembers(
                        StructDeclaration("NullEvents")
                            .AddModifiers(Token(SyntaxKind.InternalKeyword))
                            .AddAttributeLists(CreateEditorBrowsableNeverAttributeList())));

    public static CompilationUnitSyntax CreateObservableEventsBootstrapExtensionsCompilationUnit(bool includeStatics) =>
        CompilationUnit()
            .AddMembers(
                NamespaceDeclaration(BootstrapNamespaceName)
                    .AddMembers(CreateBootstrapExtensionsClass(includeStatics)));

    public static CompilationUnitSyntax CreateObservableEventsStaticsShellCompilationUnit() =>
        CompilationUnit()
            .AddMembers(
                FileScopedNamespaceDeclaration(BootstrapNamespaceName)
                    .AddMembers(
                        ClassDeclaration("ObservableEventsStatics")
                            .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.PartialKeyword))));

    private static ClassDeclarationSyntax CreateBootstrapExtensionsClass(bool includeStatics)
    {
        var methods = new List<MemberDeclarationSyntax>
        {
            CreateNullReturningExtension("FromRoutedEvents"),
            CreateNullReturningExtension(
                "FromRoutedEvents",
                Parameter(Identifier("routes")).WithType(ParseTypeName("global::System.Object")),
                Parameter(Identifier("handledEventsToo"))
                    .WithType(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                    .WithDefault(EqualsValueClause(LiteralExpression(SyntaxKind.FalseLiteralExpression)))),
            CreateNullReturningExtension("FromRoutedEventHandlers"),
            CreateNullReturningExtension(
                "FromRoutedEventHandlers",
                Parameter(Identifier("routes")).WithType(ParseTypeName("global::System.Object")),
                Parameter(Identifier("handledEventsToo"))
                    .WithType(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                    .WithDefault(EqualsValueClause(LiteralExpression(SyntaxKind.FalseLiteralExpression)))),
            CreateNullReturningExtension(
                "FromAttachedRoutedEvent",
                Parameter(Identifier("routedEvent")).WithType(ParseTypeName("global::System.Object")),
                Parameter(Identifier("routes"))
                    .WithType(ParseTypeName("global::System.Object"))
                    .WithDefault(EqualsValueClause(LiteralExpression(SyntaxKind.NullLiteralExpression))),
                Parameter(Identifier("handledEventsToo"))
                    .WithType(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                    .WithDefault(EqualsValueClause(LiteralExpression(SyntaxKind.FalseLiteralExpression)))),
            CreateNullReturningExtension(
                "FromAttachedRoutedEventHandler",
                Parameter(Identifier("routedEvent")).WithType(ParseTypeName("global::System.Object")),
                Parameter(Identifier("routes"))
                    .WithType(ParseTypeName("global::System.Object"))
                    .WithDefault(EqualsValueClause(LiteralExpression(SyntaxKind.NullLiteralExpression))),
                Parameter(Identifier("handledEventsToo"))
                    .WithType(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                    .WithDefault(EqualsValueClause(LiteralExpression(SyntaxKind.FalseLiteralExpression)))),
        };

        if (includeStatics)
        {
            methods.Add(CreateObservableEventsStaticsExtension());
        }

        return ClassDeclaration("ObservableEventsBootstrapExtensions")
            .AddModifiers(
                Token(SyntaxKind.InternalKeyword),
                Token(SyntaxKind.StaticKeyword),
                Token(SyntaxKind.PartialKeyword))
            .AddMembers(methods.ToArray());
    }

    private static MethodDeclarationSyntax CreateNullReturningExtension(
        string methodName,
        params ParameterSyntax[] extraParameters)
    {
        var parameters = new List<ParameterSyntax>
        {
            Parameter(Identifier("source"))
                .WithType(NullableType(PredefinedType(Token(SyntaxKind.ObjectKeyword))))
                .AddModifiers(Token(SyntaxKind.ThisKeyword)),
        };
        parameters.AddRange(extraParameters);

        return MethodDeclaration(NullEventsType, Identifier(methodName))
            .AddAttributeLists(CreateEditorBrowsableNeverAttributeList())
            .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
            .AddParameterListParameters(parameters.ToArray())
            .WithBody(
                Block(
                    ReturnStatement(
                        DefaultExpression(NullEventsType))));
    }

    private static MethodDeclarationSyntax CreateObservableEventsStaticsExtension()
    {
        return MethodDeclaration(NullEventsType, Identifier("ObservableEventsStatics"))
            .AddAttributeLists(CreateEditorBrowsableNeverAttributeList())
            .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
            .WithTypeParameterList(TypeParameterList(SingletonSeparatedList(TypeParameter("T"))))
            .AddParameterListParameters(
                Parameter(Identifier("source"))
                    .WithType(NullableType(IdentifierName("T")))
                    .AddModifiers(Token(SyntaxKind.ThisKeyword)))
            .WithBody(
                Block(
                    ReturnStatement(
                        DefaultExpression(NullEventsType))));
    }

    private static AttributeListSyntax CreateEditorBrowsableNeverAttributeList() =>
        AttributeList(
            SingletonSeparatedList(
                Attribute(ParseName("global::System.ComponentModel.EditorBrowsable"))
                    .WithArgumentList(
                        AttributeArgumentList(
                            SingletonSeparatedList(
                                AttributeArgument(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        ParseName("global::System.ComponentModel.EditorBrowsableState"),
                                        IdentifierName("Never"))))))));
}
