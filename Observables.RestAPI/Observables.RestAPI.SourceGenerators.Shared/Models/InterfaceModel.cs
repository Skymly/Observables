namespace Observables.RestAPI.Generators;

internal sealed record InterfaceModel(
    string FileName,
    string ClassName,
    string Ns,
    string ClassDeclaration,
    string InterfaceDisplayName,
    string ClassSuffix,
    ImmutableEquatableArray<TypeConstraint> Constraints,
    ImmutableEquatableArray<string> MemberNames,
    ImmutableEquatableArray<MethodModel> NonHttpMethods,
    ImmutableEquatableArray<MethodModel> HttpMethods,
    ImmutableEquatableArray<MethodModel> DerivedHttpMethods,
    Nullability Nullability,
    bool DisposeMethod
);

internal enum Nullability : byte
{
    Enabled,
    Disabled,
    None
}
