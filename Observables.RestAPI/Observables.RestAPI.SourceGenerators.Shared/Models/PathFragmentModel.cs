namespace Observables.RestAPI.Generators;

/// <summary>
/// A fragment of a REST API path template. Either a constant string or a reference
/// to a parameter that should be interpolated.
/// </summary>
internal readonly record struct PathFragmentModel(
    string? ConstantValue,
    int ParameterIndex,
    bool IsConstant
)
{
    public static PathFragmentModel Constant(string value) =>
        new(value, -1, true);

    public static PathFragmentModel Parameter(int index) =>
        new(null, index, false);
}
