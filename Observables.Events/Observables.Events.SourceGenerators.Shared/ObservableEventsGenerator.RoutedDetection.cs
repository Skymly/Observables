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

    private static bool HasWpfRoutedClrEvents(INamedTypeSymbol type, Compilation compilation)
    {
        return GetPublicInstanceEventsFromTypeAndBases(type)
            .Any(evt => TryGetWpfRoutedClrEventField(evt, compilation, out _, out _));
    }

    private static bool TryGetWpfRoutedClrEventField(
        IEventSymbol evt,
        Compilation compilation,
        out IFieldSymbol routedEventField,
        out INamedTypeSymbol eventArgsType)
    {
        routedEventField = null!;
        eventArgsType = null!;

        var routedEventType = compilation.GetTypeByMetadataName("System.Windows.RoutedEvent");
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
                    || field.Type is not INamedTypeSymbol fieldType)
                {
                    continue;
                }

                var normalizedFieldType = fieldType.WithNullableAnnotation(NullableAnnotation.None);
                if (!SymbolEqualityComparer.Default.Equals(normalizedFieldType, routedEventType)
                    || !TryGetWpfRoutedEventArgsType(evt, compilation, out INamedTypeSymbol argsType))
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

    private static bool TryGetWpfRoutedEventArgsType(
        IEventSymbol evt,
        Compilation compilation,
        out INamedTypeSymbol eventArgsType)
    {
        eventArgsType = null!;

        if (evt.Type is INamedTypeSymbol { DelegateInvokeMethod: { } invoke }
            && invoke.ReturnsVoid
            && invoke.Parameters.Length == 2
            && invoke.Parameters[0].RefKind == RefKind.None
            && invoke.Parameters[1].RefKind == RefKind.None
            && invoke.Parameters[0].Type.SpecialType == SpecialType.System_Object
            && invoke.Parameters[1].Type is INamedTypeSymbol argsFromHandler)
        {
            var routedEventArgs = compilation.GetTypeByMetadataName("System.Windows.RoutedEventArgs");
            if (routedEventArgs is not null && IsOrDerivedFrom(argsFromHandler, routedEventArgs))
            {
                eventArgsType = argsFromHandler;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetAvaloniaRoutedClrEventField(
        IEventSymbol evt,
        Compilation compilation,
        out IFieldSymbol routedEventField,
        out INamedTypeSymbol eventArgsType)
    {
        routedEventField = null!;
        eventArgsType = null!;

        var routedEventGenericType = compilation.GetTypeByMetadataName("Avalonia.Interactivity.RoutedEvent`1");
        var routedEventNonGenericType = compilation.GetTypeByMetadataName("Avalonia.Interactivity.RoutedEvent");
        if (routedEventGenericType is null && routedEventNonGenericType is null)
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
                    || field.Type is not INamedTypeSymbol fieldType)
                {
                    continue;
                }

                var normalizedFieldType = fieldType.WithNullableAnnotation(NullableAnnotation.None);

                if (routedEventGenericType is not null
                    && SymbolEqualityComparer.Default.Equals(normalizedFieldType.OriginalDefinition, routedEventGenericType)
                    && fieldType.TypeArguments.Length == 1
                    && fieldType.TypeArguments[0] is INamedTypeSymbol genericArgsType)
                {
                    routedEventField = field;
                    eventArgsType = genericArgsType;
                    return true;
                }

                if (routedEventNonGenericType is not null
                    && SymbolEqualityComparer.Default.Equals(normalizedFieldType, routedEventNonGenericType)
                    && TryGetAvaloniaRoutedEventArgsType(evt, compilation, out INamedTypeSymbol nonGenericArgsType))
                {
                    routedEventField = field;
                    eventArgsType = nonGenericArgsType;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetAvaloniaRoutedEventArgsType(
        IEventSymbol evt,
        Compilation compilation,
        out INamedTypeSymbol eventArgsType)
    {
        eventArgsType = null!;

        var routedEventArgs = compilation.GetTypeByMetadataName("Avalonia.Interactivity.RoutedEventArgs");
        if (evt.Type is INamedTypeSymbol { TypeArguments.Length: 1 } handlerType
            && handlerType.TypeArguments[0] is INamedTypeSymbol argsFromHandler)
        {
            eventArgsType = argsFromHandler;
            return true;
        }

        if (routedEventArgs is not null)
        {
            eventArgsType = routedEventArgs;
            return true;
        }

        return false;
    }


    private static bool IsOrDerivedFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    current.OriginalDefinition,
                    baseType.OriginalDefinition))
            {
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
