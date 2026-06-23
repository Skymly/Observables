namespace Observables.SourceGenerators.Shared;

/// <summary>
/// Indicates the nullable context state for a generated source file.
/// </summary>
internal enum Nullability : byte
{
    Enabled,
    Disabled,
    None,
}
