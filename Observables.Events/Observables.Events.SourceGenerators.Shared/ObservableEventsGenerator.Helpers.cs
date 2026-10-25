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
            if (tp.HasUnmanagedTypeConstraint)
                constraints.Add(SyntaxFactory.TypeConstraint(SyntaxFactory.IdentifierName("unmanaged")));
            else if (tp.HasValueTypeConstraint)
                constraints.Add(SyntaxFactory.ClassOrStructConstraint(SyntaxKind.StructConstraint));
            if (tp.HasNotNullConstraint)
                constraints.Add(SyntaxFactory.TypeConstraint(SyntaxFactory.IdentifierName("notnull")));
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

    // ── Accessibility ─────────────────────────────────────────────
    // Generated source may only name symbols the consumer compilation can see; otherwise the
    // consumer hits CS0122 (e.g. events discovered on cross-assembly internal interfaces/base
    // classes, or delegate signatures that mention internal framework types).

    /// <summary>
    /// Whether <paramref name="type"/> can be spelled in generated source: the type itself, its
    /// generic type arguments, array element, and tuple element types must all be accessible to
    /// the consuming assembly. Type parameters are always nameable (declared by the consumer or
    /// by the generated member itself).
    /// </summary>
    private static bool IsTypeNameable(ITypeSymbol type, Compilation compilation)
    {
        if (type.TypeKind == TypeKind.TypeParameter)
            return true;

        if (!compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
            return false;

        switch (type)
        {
            case IArrayTypeSymbol array:
                return IsTypeNameable(array.ElementType, compilation);
            case IPointerTypeSymbol pointer:
                return IsTypeNameable(pointer.PointedAtType, compilation);
            case INamedTypeSymbol named:
                foreach (var typeArgument in named.TypeArguments)
                {
                    if (!IsTypeNameable(typeArgument, compilation))
                        return false;
                }

                if (named.IsTupleType)
                {
                    foreach (var element in named.TupleElements)
                    {
                        if (!IsTypeNameable(element.Type, compilation))
                            return false;
                    }
                }

                return true;
            default:
                return true;
        }
    }

    /// <summary>
    /// Whether an event's delegate signature can be spelled in generated source — the delegate
    /// type itself plus every invoke-parameter type. Interface and impl emission share this gate
    /// so both sides skip exactly the same events.
    /// </summary>
    private static bool IsEventSignatureNameable(IEventSymbol evt, Compilation compilation)
    {
        if (!IsTypeNameable(evt.Type, compilation))
            return false;

        if (evt.Type is INamedTypeSymbol { DelegateInvokeMethod: { } invoke })
        {
            foreach (var parameter in invoke.Parameters)
            {
                if (!IsTypeNameable(parameter.Type, compilation))
                    return false;
            }
        }

        return true;
    }

    private static void ReportInvalidDelegate(
        IEventSymbol evt,
        Action<string, Location?, string> reportDiagnostic,
        ObservableEventsEntryKind entryKind)
    {
        var descriptorId = entryKind is ObservableEventsEntryKind.RoutedEvents or ObservableEventsEntryKind.RoutedEventHandlers
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
            ? DiagnosticDescriptors.InvalidRoutedEventHandlersDelegate.Id
            : DiagnosticDescriptors.InvalidEventHandlersDelegate.Id;
        reportDiagnostic(descriptorId, evt.Locations.FirstOrDefault(), evt.Name);
    }
}
