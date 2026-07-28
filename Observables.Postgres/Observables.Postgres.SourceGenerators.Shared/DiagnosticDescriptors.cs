namespace Observables.Postgres.Generators;

/// <summary>
/// Postgres diagnostics use the <c>OBS10xxx</c> segment (reserved in AGENTS.md).
/// Concrete descriptors land with the Listen/Notify generator golden path.
/// </summary>
internal static class DiagnosticDescriptors
{
    // OBS10xxx — reserved for Observables.Postgres (no shipped rules yet).
}
