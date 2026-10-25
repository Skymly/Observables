namespace Observables.RestAPI.Generators;

internal sealed record ParameterModel(
    string MetadataName,
    string Type,
    bool Annotation,
    ParameterKind Kind = ParameterKind.None
);
