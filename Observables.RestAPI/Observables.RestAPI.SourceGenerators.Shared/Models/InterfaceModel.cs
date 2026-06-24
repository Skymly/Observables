namespace Observables.RestAPI.Generators;

internal sealed record InterfaceModel(
    string FileName,
    string ClassName,
    string Ns,
    string ClassDeclaration,
    string InterfaceDisplayName,
    string ClassSuffix,
    ImmutableEquatableArray<TypeConstraint> Constraints,
    ImmutableEquatableArray<MethodModel> NonHttpMethods,
    ImmutableEquatableArray<MethodModel> HttpMethods,
    ImmutableEquatableArray<MethodModel> DerivedHttpMethods,
    Nullability Nullability,
    bool DisposeMethod
);
