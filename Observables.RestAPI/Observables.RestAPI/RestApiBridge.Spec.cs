using System.Collections;
using System.ComponentModel;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Net.Http;

namespace Observables.RestAPI
{
    public static partial class RestApiBridge
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public enum SlotKind : byte
        {
            Path = 1,
            Query,
            Header,
            HeaderCollection,
            Authorize,
            Property,
            Body,
            Multipart,
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public readonly struct QueryOptions
        {
            public QueryOptions(
                string? format = null,
                string? prefix = null,
                string? delimiter = null,
                bool treatAsString = false,
                int collectionFormat = 0,
                bool collectionFormatSpecified = false)
            {
                Format = format;
                Prefix = prefix;
                Delimiter = delimiter;
                TreatAsString = treatAsString;
                CollectionFormat = collectionFormat;
                CollectionFormatSpecified = collectionFormatSpecified;
            }

            QueryOptions(QueryOptions other, bool nameIsExplicit)
            {
                Format = other.Format;
                Prefix = other.Prefix;
                Delimiter = other.Delimiter;
                TreatAsString = other.TreatAsString;
                CollectionFormat = other.CollectionFormat;
                CollectionFormatSpecified = other.CollectionFormatSpecified;
                NameIsExplicit = nameIsExplicit;
            }

            /// <summary>
            /// Returns a copy with <see cref="NameIsExplicit"/> set. A method rather than another constructor
            /// overload: the constructor above is a shipped signature whose optional parameters must stay the
            /// widest public overload.
            /// </summary>
            public QueryOptions WithExplicitName() => new QueryOptions(this, nameIsExplicit: true);

            public string? Format { get; }
            public string? Prefix { get; }
            public string? Delimiter { get; }
            public bool TreatAsString { get; }
            public int CollectionFormat { get; }
            public bool CollectionFormatSpecified { get; }

            /// <summary>
            /// The key came from <c>[AliasAs]</c>, so
            /// <see cref="RestApiSettings.UrlParameterKeyFormatter"/> leaves it alone: an explicit name is the
            /// final wire name. Inferred names still go through the formatter.
            /// </summary>
            public bool NameIsExplicit { get; }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public readonly struct MethodFlags
        {
            public MethodFlags(
                bool isMultipart = false,
                string? multipartBoundary = null,
                int queryUriFormat = 1,
                int bodySerializationMethod = 0,
                bool? bodyBuffered = null,
                string[]? staticHeaders = null)
            {
                IsMultipart = isMultipart;
                MultipartBoundary = multipartBoundary;
                QueryUriFormat = queryUriFormat;
                BodySerializationMethod = bodySerializationMethod;
                BodyBuffered = bodyBuffered;
                StaticHeaders = staticHeaders;
            }

            public bool IsMultipart { get; }
            public string? MultipartBoundary { get; }
            public int QueryUriFormat { get; }
            public int BodySerializationMethod { get; }
            public bool? BodyBuffered { get; }
            public string[]? StaticHeaders { get; }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public readonly struct Binding
        {
            public Binding(
                SlotKind kind,
                int argIndex,
                string name,
                string? headerName = null,
                string? authorizeScheme = null,
                string? propertyKey = null,
                QueryOptions query = default)
            {
                Kind = kind;
                ArgIndex = argIndex;
                Name = name ?? "";
                HeaderName = headerName;
                AuthorizeScheme = authorizeScheme;
                PropertyKey = propertyKey;
                Query = query;
            }

            public SlotKind Kind { get; }
            public int ArgIndex { get; }
            public string Name { get; }
            public string? HeaderName { get; }
            public string? AuthorizeScheme { get; }
            public string? PropertyKey { get; }
            public QueryOptions Query { get; }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public readonly struct MethodSpec
        {
            public MethodSpec(
                string httpMethod,
                string pathTemplate,
                Binding[]? bindings,
                MethodFlags flags = default)
            {
                HttpMethod = httpMethod ?? "";
                PathTemplate = pathTemplate ?? "";
                Bindings = bindings is { Length: > 0 } ? (Binding[])bindings.Clone() : Array.Empty<Binding>();
                Flags = flags;
            }

            public string HttpMethod { get; }
            public string PathTemplate { get; }
            public Binding[] Bindings { get; }
            public MethodFlags Flags { get; }
        }

#if NET8_0_OR_GREATER
        [RequiresUnreferencedCode(RestTrimAnnotations.Reflection)]
        [RequiresDynamicCode(RestTrimAnnotations.Dynamic)]
#endif
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Task<T?> SendAsync<T, TBody>(
            HttpClient client,
            RestApiSettings settings,
            in MethodSpec spec,
            CancellationToken cancellationToken,
            params object?[] args)
        {
            if (client.BaseAddress == null)
                throw new InvalidOperationException("BaseAddress must be set on the HttpClient instance");

            var bound = RestApiProtocol.Bind(settings, spec, args ?? Array.Empty<object?>());
            return RestApiProtocol.CompleteAsync<T, TBody>(
                client,
                bound.Message,
                settings,
                bound.BodyBuffered,
                cancellationToken);
        }

#if NET8_0_OR_GREATER
        [RequiresUnreferencedCode(RestTrimAnnotations.Reflection)]
        [RequiresDynamicCode(RestTrimAnnotations.Dynamic)]
#endif
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Task SendVoidAsync(
            HttpClient client,
            RestApiSettings settings,
            in MethodSpec spec,
            CancellationToken cancellationToken,
            params object?[] args)
        {
            if (client.BaseAddress == null)
                throw new InvalidOperationException("BaseAddress must be set on the HttpClient instance");

            var bound = RestApiProtocol.Bind(settings, spec, args ?? Array.Empty<object?>());
            return RestApiProtocol.CompleteVoidAsync(client, bound.Message, settings, cancellationToken);
        }
    }
}
