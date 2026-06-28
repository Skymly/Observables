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
            ? ObservableEventsDiagnosticDescriptors.InvalidRoutedEventDelegate.Id
            : ObservableEventsDiagnosticDescriptors.InvalidEventDelegate.Id;
        reportDiagnostic(descriptorId, evt.Locations.FirstOrDefault(), evt.Name);
    }

    private static void ReportInvalidEventHandlersDelegate(
        IEventSymbol evt,
        Action<string, Location?, string> reportDiagnostic,
        ObservableEventsEntryKind entryKind)
    {
        var descriptorId = entryKind is ObservableEventsEntryKind.RoutedEvents or ObservableEventsEntryKind.RoutedEventHandlers
            or ObservableEventsEntryKind.AttachedRoutedEvent or ObservableEventsEntryKind.AttachedRoutedEventHandler
            ? ObservableEventsDiagnosticDescriptors.InvalidRoutedEventHandlersDelegate.Id
            : ObservableEventsDiagnosticDescriptors.InvalidEventHandlersDelegate.Id;
        reportDiagnostic(descriptorId, evt.Locations.FirstOrDefault(), evt.Name);
    }
}
