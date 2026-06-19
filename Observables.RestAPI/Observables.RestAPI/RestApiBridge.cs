using System.Collections;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.RestAPI
{
    /// <summary>
    /// Runtime bridge that provides HTTP execution helpers for source-generated REST API proxies.
    /// Generated proxy classes call into this helper to send requests and handle responses,
    /// eliminating the need for runtime reflection over interface metadata.
    /// </summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RestTrimAnnotations.Reflection)]
    [RequiresDynamicCode(RestTrimAnnotations.Dynamic)]
#endif
    public static class RestApiBridge
    {
        /// <summary>
        /// Formats a parameter value for inclusion in a URL path segment.
        /// The value is escaped via <see cref="Uri.EscapeDataString(string)"/>.
        /// </summary>
        /// <param name="value">The parameter value.</param>
        /// <param name="settings">REST API settings providing the URL parameter formatter.</param>
        /// <returns>The escaped, formatted string suitable for a URL path segment.</returns>
        public static string FormatPathParameter(object? value, RestApiSettings settings)
        {
            if (value == null)
                return string.Empty;

            var formatted = settings.UrlParameterFormatter.Format(value, NullAttributeProvider.Instance, value.GetType());
            return Uri.EscapeDataString(formatted ?? string.Empty);
        }

        /// <summary>
        /// Formats a parameter value for inclusion in a query string.
        /// </summary>
        /// <param name="value">The parameter value.</param>
        /// <param name="settings">REST API settings providing the URL parameter formatter.</param>
        /// <returns>The formatted string, or <c>null</c> if the value is <c>null</c>.</returns>
        public static string? FormatQueryValue(object? value, RestApiSettings settings)
        {
            if (value == null)
                return null;

            return settings.UrlParameterFormatter.Format(value, NullAttributeProvider.Instance, value.GetType());
        }

        /// <summary>
        /// Sends an HTTP request and returns the deserialized response body.
        /// Used by generated proxy methods that return <see cref="Task{T}"/> or <see cref="ValueTask{T}"/>.
        /// </summary>
        /// <typeparam name="T">The result type (e.g. the <c>T</c> in <c>Task&lt;T&gt;</c>).</typeparam>
        /// <typeparam name="TBody">The type to deserialize the response content to.</typeparam>
        /// <param name="client">The <see cref="HttpClient"/> to send the request with.</param>
        /// <param name="request">The pre-built <see cref="HttpRequestMessage"/>.</param>
        /// <param name="settings">REST API settings.</param>
        /// <param name="isApiResponse">Whether the return type is <see cref="IApiResponse{T}"/>.</param>
        /// <param name="bodyBuffered">Whether the request body should be buffered before sending.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The deserialized response, or an <see cref="ApiResponse{T}"/> wrapper if applicable.</returns>
        public static async Task<T?> SendAsync<T, TBody>(
            HttpClient client,
            HttpRequestMessage request,
            RestApiSettings settings,
            bool isApiResponse,
            bool bodyBuffered,
            CancellationToken cancellationToken
        )
        {
            HttpResponseMessage? response = null;
            HttpContent? content = null;
            var disposeResponse = ShouldDisposeResponse(typeof(TBody));
            try
            {
                if (request.Content != null && bodyBuffered)
                {
                    await request.Content.LoadIntoBufferAsync().ConfigureAwait(false);
                }

                try
                {
                    response = await client
                        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (!isApiResponse)
                        throw new ApiRequestException(request, request.Method, settings, ex);

                    return ApiResponse.Create<T, TBody>(
                        request,
                        null,
                        default,
                        settings,
                        new ApiRequestException(request, request.Method, settings, ex)
                    );
                }

                content = response.Content ?? new StringContent(string.Empty);
                Exception? error = null;

                if (typeof(T) != typeof(HttpResponseMessage))
                {
                    error = await settings.ExceptionFactory(response).ConfigureAwait(false);
                }

                if (isApiResponse)
                {
                    var body = default(TBody);
                    try
                    {
                        body =
                            error == null
                                ? await DeserializeContentAsync<TBody>(response, content, settings, cancellationToken)
                                    .ConfigureAwait(false)
                                : default;
                    }
                    catch (Exception ex)
                    {
                        if (settings.DeserializationExceptionFactory != null)
                            error = await settings
                                .DeserializationExceptionFactory(response, ex)
                                .ConfigureAwait(false);
                        else
                        {
                            error = await ApiException
                                .Create(
                                    "An error occured deserializing the response.",
                                    request,
                                    request.Method,
                                    response,
                                    settings,
                                    ex
                                )
                                .ConfigureAwait(false);
                        }
                    }

                    return ApiResponse.Create<T, TBody>(
                        request,
                        response,
                        body,
                        settings,
                        error as ApiException
                    );
                }
                else if (error != null)
                {
                    disposeResponse = false;
                    throw error;
                }
                else
                {
                    try
                    {
                        return await DeserializeContentAsync<T>(response, content, settings, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        if (settings.DeserializationExceptionFactory != null)
                        {
                            var customEx = await settings
                                .DeserializationExceptionFactory(response, ex)
                                .ConfigureAwait(false);
                            if (customEx != null)
                                throw customEx;
                            return default;
                        }
                        else
                        {
                            throw await ApiException
                                .Create(
                                    "An error occured deserializing the response.",
                                    request,
                                    request.Method,
                                    response,
                                    settings,
                                    ex
                                )
                                .ConfigureAwait(false);
                        }
                    }
                }
            }
            finally
            {
                if (disposeResponse)
                {
                    response?.Dispose();
                    content?.Dispose();
                }
            }
        }

        /// <summary>
        /// Sends an HTTP request that has no response body (void return).
        /// </summary>
        /// <param name="client">The <see cref="HttpClient"/> to send the request with.</param>
        /// <param name="request">The pre-built <see cref="HttpRequestMessage"/>.</param>
        /// <param name="settings">REST API settings.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static async Task SendVoidAsync(
            HttpClient client,
            HttpRequestMessage request,
            RestApiSettings settings,
            CancellationToken cancellationToken
        )
        {
            using var response = await client
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            var exception = await settings.ExceptionFactory(response).ConfigureAwait(false);
            if (exception != null)
                throw exception;
        }

        /// <summary>
        /// Serializes a body parameter to <see cref="HttpContent"/> using the configured content serializer.
        /// </summary>
        /// <typeparam name="T">The body type.</typeparam>
        /// <param name="value">The body value.</param>
        /// <param name="settings">REST API settings.</param>
        /// <param name="bodySerializationMethod">
        /// The body serialization method (mirrors <see cref="Observables.RestAPI.BodySerializationMethod"/>).
        /// <c>Default</c> sends strings as-is; <c>Json</c>/<c>Serialized</c> routes strings through the content serializer.
        /// </param>
        /// <returns>The serialized <see cref="HttpContent"/>.</returns>
        public static HttpContent SerializeBody<T>(T value, RestApiSettings settings, int bodySerializationMethod = 0)
        {
            if (value is HttpContent httpContent)
                return httpContent;

            if (value is Stream stream)
                return new StreamContent(stream);

            var method = (BodySerializationMethod)bodySerializationMethod;

            // Default + string → raw StringContent (no quotes)
            // Json/Serialized + string → ContentSerializer (gets JSON-encoded with quotes)
            if (method == BodySerializationMethod.Default && value is string s)
                return new StringContent(s);

            return settings.ContentSerializer.ToHttpContent(value);
        }

        /// <summary>
        /// Creates form-URL-encoded content from an object.
        /// </summary>
        /// <param name="value">The object to form-encode.</param>
        /// <param name="settings">REST API settings.</param>
        /// <returns><see cref="FormUrlEncodedContent"/> for the object.</returns>
        public static HttpContent CreateFormUrlEncodedContent(object value, RestApiSettings settings)
        {
            if (value is string str)
            {
                return new StringContent(
                    Uri.EscapeDataString(str),
                    Encoding.UTF8,
                    "application/x-www-form-urlencoded"
                );
            }

            return new FormUrlEncodedContent(new FormValueMultimap(value, settings));
        }

        /// <summary>
        /// Adds a multipart form item to a <see cref="MultipartFormDataContent"/>.
        /// </summary>
        /// <param name="multiPartContent">The multipart content collection.</param>
        /// <param name="fileName">Fallback file name.</param>
        /// <param name="parameterName">The parameter name / form field name.</param>
        /// <param name="itemValue">The item value (Stream, string, byte[], FileInfo, MultipartItem, HttpContent, or serializable object).</param>
        /// <param name="settings">REST API settings.</param>
        public static void AddMultipartItem(
            MultipartFormDataContent multiPartContent,
            string fileName,
            string parameterName,
            object? itemValue,
            RestApiSettings settings
        )
        {
            if (itemValue is HttpContent content)
            {
                multiPartContent.Add(content);
                return;
            }
            if (itemValue is MultipartItem multipartItem)
            {
                var httpContent = multipartItem.ToContent();
                multiPartContent.Add(
                    httpContent,
                    multipartItem.Name ?? parameterName,
                    string.IsNullOrEmpty(multipartItem.FileName) ? fileName : multipartItem.FileName
                );
                return;
            }

            if (itemValue is Stream streamValue)
            {
                var streamContent = new StreamContent(streamValue);
                multiPartContent.Add(streamContent, parameterName, fileName);
                return;
            }

            if (itemValue is string stringValue)
            {
                multiPartContent.Add(new StringContent(stringValue), parameterName);
                return;
            }

            if (itemValue is FileInfo fileInfoValue)
            {
                var fileContent = new StreamContent(fileInfoValue.OpenRead());
                multiPartContent.Add(fileContent, parameterName, fileInfoValue.Name);
                return;
            }

            if (itemValue is byte[] byteArrayValue)
            {
                var fileContent = new ByteArrayContent(byteArrayValue);
                multiPartContent.Add(fileContent, parameterName, fileName);
                return;
            }

            // Fallback to serializer
            Exception e;
            try
            {
                multiPartContent.Add(
                    settings.ContentSerializer.ToHttpContent(itemValue),
                    parameterName
                );
                return;
            }
            catch (Exception ex)
            {
                e = ex;
            }

            throw new ArgumentException(
                $"Unexpected parameter type in a Multipart request. Parameter {fileName} is of type {itemValue?.GetType().Name ?? "null"}, whereas allowed types are String, Stream, FileInfo, Byte array and anything that's JSON serializable",
                nameof(itemValue),
                e
            );
        }

        /// <summary>
        /// Adds query parameters from a simple value to the query parameter list.
        /// </summary>
        /// <param name="queryParams">The query parameter list to add to.</param>
        /// <param name="key">The query parameter key.</param>
        /// <param name="value">The query parameter value.</param>
        /// <param name="settings">REST API settings.</param>
        /// <param name="prefix">Optional prefix applied to the key as <c>{prefix}{delimiter}{key}</c>.</param>
        /// <param name="delimiter">Delimiter between prefix and key (default <c>.</c>).</param>
        /// <param name="format">Optional format string (e.g. <c>0.00</c>) applied to the value.</param>
        /// <param name="treatAsString">If true, enumerate the value as a single string rather than a collection.</param>
        /// <param name="collectionFormat">Collection format (mirrors <see cref="CollectionFormat"/>).</param>
        /// <param name="isCollectionFormatSpecified">Whether <paramref name="collectionFormat"/> was explicitly set.</param>
        public static void AddQueryParameter(
            List<KeyValuePair<string, string?>> queryParams,
            string key,
            object? value,
            RestApiSettings settings,
            string? prefix = null,
            string delimiter = ".",
            string? format = null,
            bool treatAsString = false,
            int collectionFormat = 0,
            bool isCollectionFormatSpecified = false
        )
        {
            if (value == null)
                return;

            // Apply prefix to key
            var finalKey = !string.IsNullOrWhiteSpace(prefix)
                ? $"{prefix}{delimiter}{key}"
                : key;

            // Determine effective collection format
            var effectiveFormat = isCollectionFormatSpecified
                ? (CollectionFormat)collectionFormat
                : settings.CollectionFormat;

            // Apply format to a single value
            string? FormatValue(object? v)
            {
                if (format != null && v != null)
                    return string.Format(CultureInfo.InvariantCulture, $"{{0:{format}}}", v);
                return FormatQueryValue(v, settings);
            }

            // Check if value is a collection (and not a string, and not forced as string)
            if (!treatAsString && value is not string && value is IEnumerable && value is not IDictionary)
            {
                var items = ((IEnumerable)value).Cast<object>().ToList();

                switch (effectiveFormat)
                {
                    case CollectionFormat.Multi:
                        foreach (var item in items)
                        {
                            var formatted = FormatValue(item);
                            if (formatted != null)
                                queryParams.Add(new KeyValuePair<string, string?>(finalKey, formatted));
                        }
                        return;

                    case CollectionFormat.Csv:
                    case CollectionFormat.Ssv:
                    case CollectionFormat.Tsv:
                    case CollectionFormat.Pipes:
                        var delim = effectiveFormat switch
                        {
                            CollectionFormat.Ssv => " ",
                            CollectionFormat.Tsv => "\t",
                            CollectionFormat.Pipes => "|",
                            _ => ",",
                        };
                        var joined = string.Join(delim, items.Select(FormatValue).Where(v => v != null));
                        if (joined.Length > 0)
                            queryParams.Add(new KeyValuePair<string, string?>(finalKey, joined));
                        return;

                    default:
                        // RefitParameterFormatter: delegate to settings (Multi-like behavior)
                        foreach (var item in items)
                        {
                            var formatted = FormatValue(item);
                            if (formatted != null)
                                queryParams.Add(new KeyValuePair<string, string?>(finalKey, formatted));
                        }
                        return;
                }
            }

            var v = FormatValue(value);
            if (v != null)
                queryParams.Add(new KeyValuePair<string, string?>(finalKey, v));
        }

        /// <summary>
        /// Builds a query string from a list of key-value pairs.
        /// </summary>
        /// <param name="queryParams">The query parameters.</param>
        /// <param name="uriFormat">The URI format to use for encoding.</param>
        /// <returns>The query string (without the leading <c>?</c>), or <c>null</c> if empty.</returns>
        public static string? BuildQueryString(
            List<KeyValuePair<string, string?>>? queryParams,
            UriFormat uriFormat = UriFormat.UriEscaped
        )
        {
            if (queryParams == null || queryParams.Count == 0)
                return null;

            var escape = uriFormat == UriFormat.UriEscaped;
            var sb = new StringBuilder();
            var first = true;
            foreach (var kvp in queryParams)
            {
                if (!first)
                    sb.Append('&');
                first = false;
                sb.Append(escape ? Uri.EscapeDataString(kvp.Key) : kvp.Key);
                sb.Append('=');
                if (kvp.Value != null)
                    sb.Append(escape ? Uri.EscapeDataString(kvp.Value) : kvp.Value);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Parses existing query string from a path and merges with new query parameters.
        /// </summary>
        /// <param name="path">The relative path that may contain an existing query string.</param>
        /// <param name="queryParams">New query parameters to add.</param>
        /// <param name="uriFormat">The URI format for encoding.</param>
        /// <returns>The final relative path with query string.</returns>
        public static string BuildRelativePath(
            string path,
            List<KeyValuePair<string, string?>>? queryParams,
            UriFormat uriFormat = UriFormat.UriEscaped
        )
        {
            var query = BuildQueryString(queryParams, uriFormat);
            if (query == null)
                return path;

            var qIndex = path.IndexOf('?');
            if (qIndex >= 0)
                return path + "&" + query;
            return path + "?" + query;
        }

        static async Task<T?> DeserializeContentAsync<T>(
            HttpResponseMessage response,
            HttpContent content,
            RestApiSettings settings,
            CancellationToken cancellationToken
        )
        {
            if (typeof(T) == typeof(HttpResponseMessage))
                return (T)(object)response;

            if (typeof(T) == typeof(HttpContent))
                return (T)(object)content;

            if (typeof(T) == typeof(Stream))
            {
                var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                return (T)(object)stream;
            }

            if (typeof(T) == typeof(string))
            {
                var s = await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return (T)(object)s;
            }

            return await settings.ContentSerializer
                .FromHttpContentAsync<T>(content, cancellationToken)
                .ConfigureAwait(false);
        }

        static bool ShouldDisposeResponse(Type deserializedType) =>
            deserializedType != typeof(HttpResponseMessage)
            && deserializedType != typeof(HttpContent)
            && deserializedType != typeof(Stream);

        /// <summary>
        /// A no-op <see cref="ICustomAttributeProvider"/> that returns no attributes.
        /// Used when calling <see cref="IUrlParameterFormatter.Format"/> from generated code
        /// where the compile-time attribute data is already known.
        /// </summary>
        internal sealed class NullAttributeProvider : ICustomAttributeProvider
        {
            public static readonly NullAttributeProvider Instance = new();

            NullAttributeProvider() { }

            public object[] GetCustomAttributes(bool inherit) => Array.Empty<object>();

            public object[] GetCustomAttributes(Type attributeType, bool inherit) =>
                Array.Empty<object>();

            public bool IsDefined(Type attributeType, bool inherit) => false;
        }
    }
}
