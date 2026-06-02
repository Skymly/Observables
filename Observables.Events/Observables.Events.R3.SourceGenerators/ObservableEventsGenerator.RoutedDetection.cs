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

namespace Observables.Events.R3.SourceGenerators;

public sealed partial class ObservableEventsGenerator
{
private static bool IsRoutedClrEvent(IEventSymbol evt, Compilation compilation, bool includeWpf)
{
    var fieldName = evt.Name + "Event";
    for (var current = evt.ContainingType; current is not null; current = current.BaseType)
    {
        if (current.SpecialType == SpecialType.System_Object)
        {
            break;
        }

        foreach (var member in current.GetMembers(fieldName))
        {
            if (member is not IFieldSymbol field || !field.IsStatic || field.IsImplicitlyDeclared)
            {
                continue;
            }

            var fieldType = field.Type.WithNullableAnnotation(NullableAnnotation.None);
            if ((includeWpf && IsWpfRoutedEventType(fieldType, compilation))
                || IsAvaloniaRoutedEventType(fieldType, compilation))
            {
                return true;
            }
        }
    }

    return false;
}

private static bool HasAvaloniaRoutedClrEvents(INamedTypeSymbol type, Compilation compilation)
{
    return GetPublicInstanceEventsFromTypeAndBases(type)
        .Any(evt => TryGetAvaloniaRoutedClrEventField(evt, compilation, out _, out _));
}

private static bool TryGetAvaloniaRoutedClrEventField(
    IEventSymbol evt,
    Compilation compilation,
    out IFieldSymbol routedEventField,
    out INamedTypeSymbol eventArgsType)
{
    routedEventField = null!;
    eventArgsType = null!;

    var routedEventType = compilation.GetTypeByMetadataName("Avalonia.Interactivity.RoutedEvent`1");
    if (routedEventType is null)
    {
        return false;
    }

    var fieldName = evt.Name + "Event";
    for (var current = evt.ContainingType; current is not null; current = current.BaseType)
    {
        if (current.SpecialType == SpecialType.System_Object)
        {
            break;
        }

        foreach (var member in current.GetMembers(fieldName))
        {
            if (member is not IFieldSymbol field
                || !field.IsStatic
                || field.IsImplicitlyDeclared
                || field.Type is not INamedTypeSymbol fieldType
                || !SymbolEqualityComparer.Default.Equals(fieldType.OriginalDefinition, routedEventType)
                || fieldType.TypeArguments.Length != 1
                || fieldType.TypeArguments[0] is not INamedTypeSymbol argsType)
            {
                continue;
            }

            routedEventField = field;
            eventArgsType = argsType;
            return true;
        }
    }

    return false;
}

private static bool IsWpfRoutedEventType(ITypeSymbol type, Compilation compilation)
{
    var routedEventType = compilation.GetTypeByMetadataName("System.Windows.RoutedEvent");
    return routedEventType is not null
        && SymbolEqualityComparer.Default.Equals(type.WithNullableAnnotation(NullableAnnotation.None), routedEventType);
}

private static bool IsAvaloniaRoutedEventType(ITypeSymbol type, Compilation compilation)
{
    var nonGeneric = compilation.GetTypeByMetadataName("Avalonia.Interactivity.RoutedEvent");
    if (nonGeneric is not null
        && SymbolEqualityComparer.Default.Equals(type.WithNullableAnnotation(NullableAnnotation.None), nonGeneric))
    {
        return true;
    }

    var generic = compilation.GetTypeByMetadataName("Avalonia.Interactivity.RoutedEvent`1");
    return generic is not null
        && type is INamedTypeSymbol named
        && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, generic);
}
}
