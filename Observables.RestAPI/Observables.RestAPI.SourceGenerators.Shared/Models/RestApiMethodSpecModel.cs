namespace Observables.RestAPI.Generators;

internal enum RestApiSlotKind : byte
{
    Path = 1,
    Query = 2,
    Header = 3,
    HeaderCollection = 4,
    Authorize = 5,
    Property = 6,
    Body = 7,
    Multipart = 8,
}

internal sealed record RestApiBindingModel(
    RestApiSlotKind Kind,
    int ArgIndex,
    string Name,
    string? HeaderName = null,
    string? AuthorizeScheme = null,
    string? PropertyKey = null,
    string? QueryFormat = null,
    string? QueryPrefix = null,
    string QueryDelimiter = ".",
    bool QueryTreatAsString = false,
    int QueryCollectionFormat = 0,
    bool QueryIsCollectionFormatSpecified = false,
    // True when Name came from [AliasAs] rather than the parameter name. The runtime key
    // formatter applies to inferred names only; an explicit one is the final wire name.
    bool NameIsExplicit = false
);

internal sealed record RestApiMethodSpecModel(
    string HttpMethod,
    string PathTemplate,
    ImmutableEquatableArray<RestApiBindingModel> Bindings,
    bool IsMultipart = false,
    string MultipartBoundary = "----MyGreatBoundary",
    int QueryUriFormat = 1,
    int BodySerializationMethod = 0,
    bool? BodyBuffered = null,
    ImmutableEquatableArray<string> StaticHeaders = default!
)
{
    public static RestApiMethodSpecModel Empty { get; } = new(
        "",
        "",
        ImmutableEquatableArray<RestApiBindingModel>.Empty,
        StaticHeaders: ImmutableEquatableArray<string>.Empty);
}
