using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Text;

namespace Observables.RestAPI
{
    internal sealed class RestApiBoundRequest
    {
        public RestApiBoundRequest(HttpRequestMessage message, bool bodyBuffered)
        {
            Message = message;
            BodyBuffered = bodyBuffered;
        }

        public HttpRequestMessage Message { get; }
        public bool BodyBuffered { get; }
        public string RelativeUri => Message.RequestUri?.OriginalString ?? "";
    }

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RestTrimAnnotations.Reflection)]
    [RequiresDynamicCode(RestTrimAnnotations.Dynamic)]
#endif
    internal static class RestApiProtocol
    {
        internal static RestApiBoundRequest Bind(
            RestApiSettings settings,
            RestApiBridge.MethodSpec spec,
            object?[] args)
        {
            args ??= Array.Empty<object?>();
            var path = ExpandPath(spec.PathTemplate, spec.Bindings, args, settings);

            List<KeyValuePair<string, string?>>? queryParams = null;
            foreach (var binding in spec.Bindings)
            {
                if (binding.Kind != RestApiBridge.SlotKind.Query)
                    continue;

                queryParams ??= new List<KeyValuePair<string, string?>>();
                var query = binding.Query;
                RestApiBridge.AddQueryParameter(
                    queryParams,
                    binding.Name,
                    GetArg(args, binding.ArgIndex),
                    settings,
                    prefix: query.Prefix,
                    delimiter: string.IsNullOrEmpty(query.Delimiter) ? "." : query.Delimiter!,
                    format: query.Format,
                    treatAsString: query.TreatAsString,
                    collectionFormat: query.CollectionFormat,
                    isCollectionFormatSpecified: query.CollectionFormatSpecified,
                    nameIsExplicit: query.NameIsExplicit);
            }

            if (queryParams != null)
            {
                var uriFormat = spec.Flags.QueryUriFormat == 0
                    ? UriFormat.UriEscaped
                    : (UriFormat)spec.Flags.QueryUriFormat;
                path = RestApiBridge.BuildRelativePath(path, queryParams, uriFormat);
            }

            var request = new HttpRequestMessage { Method = GetHttpMethod(spec.HttpMethod) };

            // Settings are defaults: a [Property] parameter below overwrites the same key.
            if (settings.HttpRequestMessageOptions != null)
            {
                foreach (var option in settings.HttpRequestMessageOptions)
                {
#if NET6_0_OR_GREATER
                    request.Options.Set(new HttpRequestOptionsKey<object>(option.Key), option.Value);
#else
                    request.Properties[option.Key] = option.Value;
#endif
                }
            }

#if NET6_0_OR_GREATER
            request.Version = settings.Version;
            request.VersionPolicy = settings.VersionPolicy;
#endif

            var isMultipart = spec.Flags.IsMultipart;
            MultipartFormDataContent? multipart = null;
            if (isMultipart)
            {
                var boundary = string.IsNullOrEmpty(spec.Flags.MultipartBoundary)
                    ? RestApiDefaults.MultipartBoundary
                    : spec.Flags.MultipartBoundary!;
                multipart = new MultipartFormDataContent(boundary);
                request.Content = multipart;
            }

            var pendingContentHeaders = new List<KeyValuePair<string, string?>>();
            if (spec.Flags.StaticHeaders != null)
            {
                foreach (var header in spec.Flags.StaticHeaders)
                    TryAddStaticHeader(request, header, pendingContentHeaders);
            }

            var hasBody = isMultipart;
            foreach (var binding in spec.Bindings)
            {
                var value = GetArg(args, binding.ArgIndex);
                switch (binding.Kind)
                {
                    case RestApiBridge.SlotKind.Header:
                        var headerName = binding.HeaderName ?? binding.Name;
                        AddRequestOrContentHeader(
                            request,
                            headerName,
                            RestApiBridge.FormatQueryValue(value, settings),
                            pendingContentHeaders);
                        break;
                    case RestApiBridge.SlotKind.HeaderCollection:
                        AddHeaderCollection(request, value, pendingContentHeaders);
                        break;
                    case RestApiBridge.SlotKind.Authorize:
                        var scheme = string.IsNullOrEmpty(binding.AuthorizeScheme)
                            ? "Bearer"
                            : binding.AuthorizeScheme!;
                        request.Headers.TryAddWithoutValidation("Authorization", scheme + " " + value);
                        break;
                    case RestApiBridge.SlotKind.Property:
                        var propKey = binding.PropertyKey ?? binding.Name;
#if NET6_0_OR_GREATER
                        request.Options.Set(new HttpRequestOptionsKey<object>(propKey), value!);
#else
                        request.Properties[propKey] = value!;
#endif
                        break;
                    case RestApiBridge.SlotKind.Body:
                        hasBody = true;
                        var previousContent = request.Content;
                        request.Content = (BodySerializationMethod)spec.Flags.BodySerializationMethod
                            == BodySerializationMethod.UrlEncoded
                            ? RestApiBridge.CreateFormUrlEncodedContent(value!, settings)
                            : RestApiBridge.SerializeBody(value!, settings, spec.Flags.BodySerializationMethod);
                        if (!ReferenceEquals(previousContent, request.Content))
                        {
                            previousContent?.Dispose();
                        }
                        break;
                    case RestApiBridge.SlotKind.Multipart:
                        hasBody = true;
                        if (multipart != null)
                            RestApiBridge.AddMultipartItem(multipart, binding.Name, binding.Name, value, settings);
                        break;
                }
            }

            ApplyPendingContentHeaders(request, pendingContentHeaders);

            var relative = ToRelativePath(path);
            request.RequestUri = string.IsNullOrEmpty(relative)
                ? null
                : new Uri(relative, UriKind.Relative);
            var bodyBuffered = spec.Flags.BodyBuffered ?? (hasBody && settings.Buffered);
            return new RestApiBoundRequest(request, bodyBuffered);
        }

        internal static Task<T?> CompleteAsync<T, TBody>(
            HttpClient client,
            HttpRequestMessage request,
            RestApiSettings settings,
            bool bodyBuffered,
            CancellationToken cancellationToken) =>
            RestApiBridge.SendAsync<T, TBody>(client, request, settings, bodyBuffered, cancellationToken);

        internal static Task CompleteVoidAsync(
            HttpClient client,
            HttpRequestMessage request,
            RestApiSettings settings,
            CancellationToken cancellationToken) =>
            RestApiBridge.SendVoidAsync(client, request, settings, cancellationToken);

        static string ToRelativePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            if (path[0] == '/' && (path.Length == 1 || path[1] != '/'))
            {
                return path.Substring(1);
            }

            return path;
        }

        static object? GetArg(object?[] args, int index)
        {
            if ((uint)index >= (uint)args.Length)
            {
                throw new InvalidOperationException(
                    "REST API argument list does not match the compiled method spec.");
            }

            return args[index];
        }

        static string ExpandPath(
            string template,
            RestApiBridge.Binding[] bindings,
            object?[] args,
            RestApiSettings settings)
        {
            Dictionary<string, RestApiBridge.Binding>? byName = null;
            foreach (var binding in bindings)
            {
                if (binding.Kind != RestApiBridge.SlotKind.Path)
                    continue;
                byName ??= new Dictionary<string, RestApiBridge.Binding>(StringComparer.Ordinal);
                byName[binding.Name] = binding;
            }

            if (byName == null)
                return template;

            var sb = new StringBuilder();
            for (var i = 0; i < template.Length; i++)
            {
                if (template[i] != '{')
                {
                    sb.Append(template[i]);
                    continue;
                }

                var close = template.IndexOf('}', i + 1);
                if (close < 0)
                {
                    sb.Append(template, i, template.Length - i);
                    break;
                }

                var name = template.Substring(i + 1, close - i - 1);
                if (byName.TryGetValue(name, out var binding))
                    sb.Append(RestApiBridge.FormatPathParameter(GetArg(args, binding.ArgIndex), settings));
                else
                    sb.Append('{').Append(name).Append('}');
                i = close;
            }

            return sb.ToString();
        }

        static HttpMethod GetHttpMethod(string httpMethod) => httpMethod switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "HEAD" => HttpMethod.Head,
            "PATCH" => new HttpMethod("PATCH"),
            "OPTIONS" => new HttpMethod("OPTIONS"),
            _ => string.IsNullOrWhiteSpace(httpMethod) ? HttpMethod.Get : new HttpMethod(httpMethod),
        };

        static void AddHeaderCollection(
            HttpRequestMessage request,
            object? value,
            List<KeyValuePair<string, string?>> pendingContentHeaders)
        {
            if (value is not IEnumerable items)
                return;

            foreach (var item in items)
            {
                switch (item)
                {
                    case KeyValuePair<string, string> pair:
                        AddRequestOrContentHeader(request, pair.Key, pair.Value, pendingContentHeaders);
                        break;
                    case DictionaryEntry entry:
                        if (entry.Key is string key)
                            AddRequestOrContentHeader(
                                request,
                                key,
                                entry.Value as string ?? entry.Value?.ToString(),
                                pendingContentHeaders);
                        break;
                }
            }
        }

        static void TryAddStaticHeader(
            HttpRequestMessage request,
            string header,
            List<KeyValuePair<string, string?>> pendingContentHeaders)
        {
            var colonIdx = header.IndexOf(':');
            if (colonIdx <= 0)
            {
                throw new InvalidOperationException(
                    $"Static header '{header}' must be in 'Name: value' form.");
            }

            var hKey = header.Substring(0, colonIdx).Trim();
            var hVal = colonIdx + 1 < header.Length ? header.Substring(colonIdx + 1).Trim() : "";
            AddRequestOrContentHeader(request, hKey, hVal, pendingContentHeaders);
        }

        static void AddRequestOrContentHeader(
            HttpRequestMessage request,
            string name,
            string? value,
            List<KeyValuePair<string, string?>> pendingContentHeaders)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("HTTP header name must be non-empty.");
            }

            if (request.Headers.TryAddWithoutValidation(name, value))
            {
                return;
            }

            if (request.Content is not null)
            {
                SetContentHeader(request.Content, name, value);
                return;
            }

            if (IsContentHeaderName(name))
            {
                pendingContentHeaders.Add(new KeyValuePair<string, string?>(name, value));
                return;
            }

            throw new InvalidOperationException($"Cannot set HTTP header '{name}'.");
        }

        static void ApplyPendingContentHeaders(
            HttpRequestMessage request,
            List<KeyValuePair<string, string?>> pendingContentHeaders)
        {
            if (pendingContentHeaders.Count == 0)
            {
                return;
            }

            if (request.Content is null)
            {
                throw new InvalidOperationException(
                    $"Cannot set HTTP header '{pendingContentHeaders[0].Key}' without a request body.");
            }

            foreach (var header in pendingContentHeaders)
            {
                SetContentHeader(request.Content, header.Key, header.Value);
            }
        }

        static void SetContentHeader(HttpContent content, string name, string? value)
        {
            content.Headers.Remove(name);
            if (!content.Headers.TryAddWithoutValidation(name, value))
            {
                throw new InvalidOperationException($"Cannot set HTTP header '{name}'.");
            }
        }

        static bool IsContentHeaderName(string name)
        {
            using var probe = new ByteArrayContent(Array.Empty<byte>());
            return probe.Headers.TryAddWithoutValidation(name, "x");
        }
    }
}

