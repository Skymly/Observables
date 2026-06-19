namespace Observables.RestAPI.Generators;

/// <summary>
/// Classifies how a REST API interface parameter is bound to the HTTP request.
/// </summary>
internal enum ParameterKind : byte
{
    /// <summary>Parameter is not bound to any specific HTTP request part (fallback).</summary>
    None,

    /// <summary>Parameter is interpolated into the URL path (e.g. <c>{id}</c>).</summary>
    Path,

    /// <summary>Parameter is added to the query string.</summary>
    Query,

    /// <summary>Parameter is sent as the request body.</summary>
    Body,

    /// <summary>Parameter is added as a request header.</summary>
    Header,

    /// <summary>Parameter is a header collection (dictionary of headers).</summary>
    HeaderCollection,

    /// <summary>Parameter provides an Authorization header value.</summary>
    Authorize,

    /// <summary>Parameter is stored in <see cref="System.Net.Http.HttpRequestMessage.Properties"/>.</summary>
    Property,

    /// <summary>Parameter is a multipart form item.</summary>
    Multipart,

    /// <summary>Parameter is a <see cref="System.Threading.CancellationToken"/>.</summary>
    CancellationToken,
}

/// <summary>
/// Mirrors Observables.RestAPI.BodySerializationMethod for compile-time use.
/// Values must match the runtime enum.
/// </summary>
internal enum BodySerializationMethod : int
{
    Default = 0,
    Json = 1,
    UrlEncoded = 2,
    Serialized = 3,
}
