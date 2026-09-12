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

namespace Observables.Events.Generators;

public sealed partial class ObservableEventsGenerator
{
    private static string GetGenericConstraintTargetHintName(GenericConstraintTarget target) =>
        $"GenericConstraints_{ToIdentifier(target.Key)}";


    private static SyntaxList<TypeParameterConstraintClauseSyntax> CreateSourceTypeConstraintClauses(INamedTypeSymbol type)
    {
        if (!type.IsGenericType)
            return default;

        var clauses = new List<TypeParameterConstraintClauseSyntax>();
        foreach (var tp in type.TypeParameters)
        {
            var constraints = new List<TypeParameterConstraintSyntax>();
            if (tp.HasReferenceTypeConstraint)
                constraints.Add(SyntaxFactory.ClassOrStructConstraint(SyntaxKind.ClassConstraint));
            if (tp.HasValueTypeConstraint)
                constraints.Add(SyntaxFactory.ClassOrStructConstraint(SyntaxKind.StructConstraint));
            if (tp.HasNotNullConstraint)
                constraints.Add(SyntaxFactory.TypeConstraint(SyntaxFactory.IdentifierName("notnull")));
            if (tp.HasUnmanagedTypeConstraint)
                constraints.Add(SyntaxFactory.TypeConstraint(SyntaxFactory.IdentifierName("unmanaged")));
            foreach (var constraintType in tp.ConstraintTypes)
                constraints.Add(SyntaxFactory.TypeConstraint(SyntaxFactory.ParseTypeName(ObservableEventsConstants.QualifiedType(constraintType))));
            if (tp.HasConstructorConstraint)
                constraints.Add(SyntaxFactory.ConstructorConstraint());

            if (constraints.Count == 0)
                continue;

            clauses.Add(
                SyntaxFactory.TypeParameterConstraintClause(tp.Name)
                    .WithConstraints(SyntaxFactory.SeparatedList(constraints)));
        }

        return SyntaxFactory.List(clauses);
    }

    private static TypeParameterConstraintClauseSyntax CreateGenericConstraintClauseSyntax(GenericConstraintTarget target) =>
        SyntaxFactory.TypeParameterConstraintClause("TSource")
            .WithConstraints(
                SyntaxFactory.SeparatedList<TypeParameterConstraintSyntax>(
                    target.ConstraintTypes.Select(static t =>
                        (TypeParameterConstraintSyntax)SyntaxFactory.TypeConstraint(
                            SyntaxFactory.ParseTypeName(QualifiedConstraintType(t))))));

    private static string QualifiedConstraintType(INamedTypeSymbol type) =>
        type.WithNullableAnnotation(NullableAnnotation.None).ToDisplayString(ObservableEventsConstants.FullyQualifiedNullableFormat);
    private static string GetTypeUniqueIdentifier(INamedTypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty)
            .Replace('<', '_')
            .Replace('>', '_')
            .Replace('.', '_');
    }

    private static string ToIdentifier(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }

        if (builder.Length == 0 || char.IsDigit(builder[0]))
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }

    private static void ReportInvalidDelegate(
        IEventSymbol evt,
        Action<string, Location?, string> reportDiagnostic,
        ObservableEventsEntryKind entryKind)
    {
        var descriptorId = entryKind is ObservableEventsEntryKind.RoutedEvents or ObservableEventsEntryKind.RoutedEventHandlers
            or ObservableEventsEntryKind.AttachedRoutedEvent or ObservableEventsEntryKind.AttachedRoutedEventHandler
            ? DiagnosticDescriptors.InvalidRoutedEventDelegate.Id
            : DiagnosticDescriptors.InvalidEventDelegate.Id;
        reportDiagnostic(descriptorId, evt.Locations.FirstOrDefault(), evt.Name);
    }

    private static void ReportInvalidEventHandlersDelegate(
        IEventSymbol evt,
        Action<string, Location?, string> reportDiagnostic,
        ObservableEventsEntryKind entryKind)
    {
        var descriptorId = entryKind is ObservableEventsEntryKind.RoutedEvents or ObservableEventsEntryKind.RoutedEventHandlers
            or ObservableEventsEntryKind.AttachedRoutedEvent or ObservableEventsEntryKind.AttachedRoutedEventHandler
            ? DiagnosticDescriptors.InvalidRoutedEventHandlersDelegate.Id
            : DiagnosticDescriptors.InvalidEventHandlersDelegate.Id;
        reportDiagnostic(descriptorId, evt.Locations.FirstOrDefault(), evt.Name);
    }
}
