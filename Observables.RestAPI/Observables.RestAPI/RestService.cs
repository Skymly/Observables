using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net.Http;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.RestAPI
{
    /// <summary>
    /// Creates REST API client implementations for declarative HTTP interfaces.
    /// </summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RestTrimAnnotations.Reflection)]
    [RequiresDynamicCode(RestTrimAnnotations.Dynamic)]
#endif
    public static class RestService
    {
        static readonly ConcurrentDictionary<Type, Type> TypeMapping = new();
        static readonly ConcurrentDictionary<Type, Func<HttpClient, RestApiSettings?, object>> GeneratedFactories = new();

        /// <summary>
        /// Registers a source-generated REST API client implementation factory.
        /// </summary>
        /// <param name="interfaceType">The REST API interface type.</param>
        /// <param name="factory">The generated implementation factory.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
#if NET8_0_OR_GREATER
        public static void RegisterGeneratedFactory(
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.PublicProperties
            )] Type interfaceType,
            Func<HttpClient, RestApiSettings?, object> factory
        )
#else
        public static void RegisterGeneratedFactory(
            Type interfaceType,
            Func<HttpClient, RestApiSettings?, object> factory
        )
#endif
        {
            if (interfaceType is null)
                throw new ArgumentNullException(nameof(interfaceType));
            if (factory is null)
                throw new ArgumentNullException(nameof(factory));

            GeneratedFactories[interfaceType] = factory;
        }

        /// <summary>
        /// Generate a REST API client implementation of the specified interface.
        /// </summary>
        /// <typeparam name="T">Interface to create the implementation for.</typeparam>
        /// <param name="client">The <see cref="HttpClient"/> the implementation will use to send requests.</param>
        /// <param name="settings"><see cref="RestApiSettings"/> to use to configure the HttpClient.</param>
        /// <returns>An instance that implements <typeparamref name="T"/>.</returns>
#if NET8_0_OR_GREATER
        public static T For<
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.PublicProperties
            )] T>(HttpClient client, RestApiSettings? settings)
#else
        public static T For<T>(HttpClient client, RestApiSettings? settings)
#endif
            => (T)For(typeof(T), client, settings);

        /// <summary>
        /// Generate a REST API client implementation of the specified interface.
        /// </summary>
        /// <typeparam name="T">Interface to create the implementation for.</typeparam>
        /// <param name="client">The <see cref="HttpClient"/> the implementation will use to send requests.</param>
        /// <returns>An instance that implements <typeparamref name="T"/>.</returns>
#if NET8_0_OR_GREATER
        public static T For<
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.PublicProperties
            )] T>(HttpClient client) => For<T>(client, (RestApiSettings?)null);
#else
        public static T For<T>(HttpClient client) => For<T>(client, (RestApiSettings?)null);
#endif

        /// <summary>
        /// Generate a REST API client implementation of the specified interface.
        /// </summary>
        /// <typeparam name="T">Interface to create the implementation for.</typeparam>
        /// <param name="hostUrl">Base address the implementation will use.</param>
        /// <param name="settings"><see cref="RestApiSettings"/> to use to configure the HttpClient.</param>
        /// <returns>An instance that implements <typeparamref name="T"/>.</returns>
#if NET8_0_OR_GREATER
        public static T For<
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.PublicProperties
            )] T>(string hostUrl, RestApiSettings? settings)
#else
        public static T For<T>(string hostUrl, RestApiSettings? settings)
#endif
        {
            var client = CreateHttpClient(hostUrl, settings);
            return For<T>(client, settings);
        }

        /// <summary>
        /// Generate a REST API client implementation of the specified interface.
        /// </summary>
        /// <typeparam name="T">Interface to create the implementation for.</typeparam>
        /// <param name="hostUrl">Base address the implementation will use.</param>
        /// <returns>An instance that implements <typeparamref name="T"/>.</returns>
#if NET8_0_OR_GREATER
        public static T For<
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.PublicProperties
            )] T>(string hostUrl) => For<T>(hostUrl, null);
#else
        public static T For<T>(string hostUrl) => For<T>(hostUrl, null);
#endif

        /// <summary>
        /// Generate a REST API client implementation of the specified interface.
        /// </summary>
        /// <param name="interfaceType">Interface to create the implementation for.</param>
        /// <param name="client">The <see cref="HttpClient"/> the implementation will use to send requests.</param>
        /// <param name="settings"><see cref="RestApiSettings"/> to use to configure the HttpClient.</param>
        /// <returns>An instance that implements <paramref name="interfaceType"/>.</returns>
#if NET8_0_OR_GREATER
        public static object For(
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.PublicProperties
            )] Type interfaceType,
            HttpClient client,
            RestApiSettings? settings
        )
#else
        public static object For(
            Type interfaceType,
            HttpClient client,
            RestApiSettings? settings
        )
#endif
        {
            if (GeneratedFactories.TryGetValue(interfaceType, out var factory))
            {
                return factory(client, settings);
            }

#if NET8_0_OR_GREATER
            var generatedType = TypeMapping.GetOrAdd(
                interfaceType,
                static ([DynamicallyAccessedMembers(
                    DynamicallyAccessedMemberTypes.PublicMethods |
                    DynamicallyAccessedMemberTypes.PublicProperties)] Type type) => GetGeneratedType(type));
#else
            var generatedType = TypeMapping.GetOrAdd(interfaceType, GetGeneratedType);
#endif

            return Activator.CreateInstance(generatedType, client, settings)!;
        }

        /// <summary>
        /// Generate a REST API client implementation of the specified interface.
        /// </summary>
        /// <param name="interfaceType">Interface to create the implementation for.</param>
        /// <param name="client">The <see cref="HttpClient"/> the implementation will use to send requests.</param>
        /// <returns>An instance that implements <paramref name="interfaceType"/>.</returns>
#if NET8_0_OR_GREATER
        public static object For(
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.PublicProperties
            )] Type interfaceType,
            HttpClient client
        ) => For(interfaceType, client, (RestApiSettings?)null);
#else
        public static object For(Type interfaceType, HttpClient client) =>
            For(interfaceType, client, (RestApiSettings?)null);
#endif

        /// <summary>
        /// Generate a REST API client implementation of the specified interface.
        /// </summary>
        /// <param name="interfaceType">Interface to create the implementation for.</param>
        /// <param name="hostUrl">Base address the implementation will use.</param>
        /// <param name="settings"><see cref="RestApiSettings"/> to use to configure the HttpClient.</param>
        /// <returns>An instance that implements <paramref name="interfaceType"/>.</returns>
#if NET8_0_OR_GREATER
        public static object For(
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.PublicProperties
            )] Type interfaceType,
            string hostUrl,
            RestApiSettings? settings
        )
#else
        public static object For(Type interfaceType, string hostUrl, RestApiSettings? settings)
#endif
        {
            var client = CreateHttpClient(hostUrl, settings);
            return For(interfaceType, client, settings);
        }

        /// <summary>
        /// Generate a REST API client implementation of the specified interface.
        /// </summary>
        /// <param name="interfaceType">Interface to create the implementation for.</param>
        /// <param name="hostUrl">Base address the implementation will use.</param>
        /// <returns>An instance that implements <paramref name="interfaceType"/>.</returns>
#if NET8_0_OR_GREATER
        public static object For(
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.PublicProperties
            )] Type interfaceType,
            string hostUrl
        ) => For(interfaceType, hostUrl, null);
#else
        public static object For(Type interfaceType, string hostUrl) =>
            For(interfaceType, hostUrl, null);
#endif

        /// <summary>
        /// Create an <see cref="HttpClient"/> with <paramref name="hostUrl"/> as the base address.
        /// </summary>
        /// <param name="hostUrl">Base address.</param>
        /// <param name="settings"><see cref="RestApiSettings"/> to use to configure the HttpClient.</param>
        /// <returns>A <see cref="HttpClient"/> with the various parameters provided.</returns>
        /// <exception cref="ArgumentException"></exception>
        public static HttpClient CreateHttpClient(string hostUrl, RestApiSettings? settings)
        {
            if (string.IsNullOrWhiteSpace(hostUrl))
            {
                throw new ArgumentException(
                    $"`{nameof(hostUrl)}` must not be null or whitespace.",
                    nameof(hostUrl)
                );
            }

            HttpMessageHandler? innerHandler = null;
            if (settings != null)
            {
                if (settings.HttpMessageHandlerFactory != null)
                {
                    innerHandler = settings.HttpMessageHandlerFactory();
                }

                if (settings.AuthorizationHeaderValueGetter != null)
                {
                    innerHandler = new AuthenticatedHttpClientHandler(
                        settings.AuthorizationHeaderValueGetter,
                        innerHandler
                    );
                }
            }

            return new HttpClient(innerHandler ?? new HttpClientHandler())
            {
                BaseAddress = new Uri(hostUrl.TrimEnd('/'))
            };
        }

#if NET8_0_OR_GREATER
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        static Type GetGeneratedType(
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.PublicProperties
            )] Type interfaceType
        )
#else
        static Type GetGeneratedType(Type interfaceType)
#endif
        {
            var typeName = UniqueName.ForType(interfaceType);

            var generatedType = GetGeneratedImplementationType(typeName);

            if (generatedType == null)
            {
                var message =
                    interfaceType.Name
                    + " doesn't look like a Rest API interface. Make sure it has at least one "
                    + "method with a Rest API HTTP method attribute, the Observables.RestAPI source generator is installed in the project, "
                    + "and your build produced the generated client. For Native AOT or trimmed apps, prefer generated clients "
                    + "plus source-generated System.Text.Json metadata.";

                throw new InvalidOperationException(message);
            }

            return generatedType;
        }

#if NET8_0_OR_GREATER
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2057:UnrecognizedTypeName",
            Justification = "Type name is produced by the Observables.RestAPI source generator for the interface.")]
#endif
        static Type? GetGeneratedImplementationType(string typeName) =>
            Type.GetType(typeName, throwOnError: false);
    }
}
