namespace Observables.RestAPI.Generators;

internal sealed record ParameterModel(
    string MetadataName,
    string Type,
    bool Annotation,
    bool IsGeneric,
    ParameterKind Kind = ParameterKind.None,
    string? AliasAs = null,
    string? HeaderName = null,
    string? AuthorizeScheme = null,
    string? PropertyKey = null,
    string? QueryFormat = null,
    string? QueryPrefix = null,
    string QueryDelimiter = ".",
    bool QueryTreatAsString = false,
    int QueryCollectionFormat = 0,
    bool QueryIsCollectionFormatSpecified = false
);
