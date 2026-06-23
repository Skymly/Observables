#if ROSLYN_4
using Microsoft.CodeAnalysis;

namespace Observables.SourceGenerators.Shared;

/// <summary>
/// Shared extension methods for <see cref="IncrementalGeneratorInitializationContext"/>.
/// Domain-specific generators keep their own <c>EmitSource</c> overloads.
/// </summary>
internal static class IncrementalValuesProviderExtensions
{
    /// <summary>
    /// Registers a source output node that reports a batch of diagnostics.
    /// </summary>
    public static void ReportDiagnostics(
        this IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<ImmutableEquatableArray<Diagnostic>> diagnostics)
    {
        context.RegisterSourceOutput(
            diagnostics,
            static (context, diagnostics) =>
            {
                foreach (var diagnostic in diagnostics)
                {
                    context.ReportDiagnostic(diagnostic);
                }
            });
    }
}
#endif
