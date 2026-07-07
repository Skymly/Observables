#if ROSLYN_4
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Observables.SourceGenerators.Shared;

/// <summary>
/// Converts unexpected generator exceptions into domain diagnostics instead of crashing compilation.
/// </summary>
internal static class GeneratorFailSafe
{
    public static Diagnostic CreateInternalError(DiagnosticDescriptor descriptor, Exception exception) =>
        Diagnostic.Create(descriptor, Location.None, exception.GetType().Name, exception.Message);

    public static void TryEmit(Action emit, Action<Diagnostic> report, DiagnosticDescriptor internalErrorDescriptor)
    {
        try
        {
            emit();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            report(CreateInternalError(internalErrorDescriptor, exception));
        }
    }

    public static (List<Diagnostic> diagnostics, TModel model) ExecuteParse<TModel>(
        Func<(List<Diagnostic> diagnostics, TModel model)> parse,
        DiagnosticDescriptor internalErrorDescriptor,
        Func<TModel> emptyModel)
    {
        try
        {
            return parse();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return (new List<Diagnostic> { CreateInternalError(internalErrorDescriptor, exception) }, emptyModel());
        }
    }
}
#endif
