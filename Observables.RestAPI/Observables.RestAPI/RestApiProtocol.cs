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
                    isCollectionFormatSpecified: query.CollectionFormatSpecified);
            }

            if (queryParams != null)
            {
                var uriFormat = spec.Flags.QueryUriFormat == 0
                    ? UriFormat.UriEscaped
                    : (UriFormat)spec.Flags.QueryUriFormat;
                path = RestApiBridge.BuildRelativePath(path, queryParams, uriFormat);
            }

            var request = new HttpRequestMessage { Method = GetHttpMethod(spec.HttpMethod) };

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

            if (spec.Flags.StaticHeaders != null)
            {
                foreach (var header in spec.Flags.StaticHeaders)
                    TryAddStaticHeader(request, header);
            }

            var hasBody = isMultipart;
            foreach (var binding in spec.Bindings)
            {
                var value = GetArg(args, binding.ArgIndex);
                switch (binding.Kind)
                {
                    case RestApiBridge.SlotKind.Header:
                        var headerName = binding.HeaderName ?? binding.Name;
                        request.Headers.TryAddWithoutValidation(
                            headerName,
                            RestApiBridge.FormatQueryValue(value, settings));
                        break;
                    case RestApiBridge.SlotKind.HeaderCollection:
                        AddHeaderCollection(request, value);
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
                        request.Content = (BodySerializationMethod)spec.Flags.BodySerializationMethod
                            == BodySerializationMethod.UrlEncoded
                            ? RestApiBridge.CreateFormUrlEncodedContent(value!, settings)
                            : RestApiBridge.SerializeBody(value!, settings, spec.Flags.BodySerializationMethod);
                        break;
                    case RestApiBridge.SlotKind.Multipart:
                        hasBody = true;
                        if (multipart != null)
                            RestApiBridge.AddMultipartItem(multipart, binding.Name, binding.Name, value, settings);
                        break;
                }
            }

            request.RequestUri = new Uri(path, UriKind.Relative);
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
            _ => HttpMethod.Get,
        };

        static void AddHeaderCollection(HttpRequestMessage request, object? value)
        {
            if (value is not IEnumerable items)
                return;

            foreach (var item in items)
            {
                switch (item)
                {
                    case KeyValuePair<string, string> pair:
                        request.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
                        break;
                    case DictionaryEntry entry:
                        if (entry.Key is string key)
                            request.Headers.TryAddWithoutValidation(key, entry.Value as string ?? entry.Value?.ToString());
                        break;
                }
            }
        }

        static void TryAddStaticHeader(HttpRequestMessage request, string header)
        {
            var colonIdx = header.IndexOf(':');
            if (colonIdx <= 0)
                return;

            var hKey = header.Substring(0, colonIdx).Trim();
            var hVal = colonIdx + 1 < header.Length ? header.Substring(colonIdx + 1).Trim() : "";
            request.Headers.TryAddWithoutValidation(hKey, hVal);
        }
    }
}

