using System.Net.Http;
using System.Net.Http.Headers;

namespace Observables.RestAPI
{
    public class AuthenticatedHttpClientHandler : DelegatingHandler
    {
        readonly Func<HttpRequestMessage, CancellationToken, Task<string>> getToken;
        readonly Uri? authorizationHeaderOrigin;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticatedHttpClientHandler"/> class.
        /// </summary>
        /// <param name="getToken">The function to get the authentication token.</param>
        /// <param name="innerHandler">The optional inner handler.</param>
        /// <exception cref="ArgumentNullException"><paramref name="getToken"/> must not be null.</exception>
        /// <remarks>
        /// Warning: This constructor sets the <see cref="DelegatingHandler.InnerHandler"/> to an instance
        /// of <see cref="HttpClientHandler"/>, when <paramref name="innerHandler"/> is <c>null</c>. This is
        /// a behavior which is incompatible with the <code>IHttpClientBuilder</code>.
        /// </remarks>
        public AuthenticatedHttpClientHandler(
            Func<HttpRequestMessage, CancellationToken, Task<string>> getToken,
            HttpMessageHandler? innerHandler = null
        )
            : this(getToken, innerHandler, authorizationHeaderOrigin: null)
        {
        }

        internal AuthenticatedHttpClientHandler(
            Func<HttpRequestMessage, CancellationToken, Task<string>> getToken,
            HttpMessageHandler? innerHandler,
            Uri? authorizationHeaderOrigin
        )
            : base(innerHandler ?? new HttpClientHandler())
        {
            this.getToken = getToken ?? throw new ArgumentNullException(nameof(getToken));
            this.authorizationHeaderOrigin = authorizationHeaderOrigin;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticatedHttpClientHandler"/> class.
        /// </summary>
        /// <param name="innerHandler">The optional inner handler.</param>
        /// <param name="getToken">The function to get the authentication token.</param>
        /// <exception cref="ArgumentNullException"><paramref name="getToken"/> must not be null.</exception>
        /// <remarks>
        /// This function doesn't set the <see cref="DelegatingHandler.InnerHandler"/> automatically to an
        /// instance of <see cref="HttpClientHandler"/> when <paramref name="innerHandler"/> is null,
        /// which is different from the old (legacy) constructor, and compliant with the behavior expected
        /// by the <code>IHttpClientBuilder</code>.
        /// </remarks>
        public AuthenticatedHttpClientHandler(
            HttpMessageHandler? innerHandler,
            Func<HttpRequestMessage, CancellationToken, Task<string>> getToken
        )
        {
            this.getToken = getToken ?? throw new ArgumentNullException(nameof(getToken));
            if (innerHandler != null)
                InnerHandler = innerHandler;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var auth = request.Headers.Authorization;
            if (!string.IsNullOrWhiteSpace(auth?.Parameter)
                || (auth is null && request.Headers.Contains("Authorization"))
                || !IsAllowedOrigin(request.RequestUri))
            {
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }

            var token = await getToken(request, cancellationToken).ConfigureAwait(false);
            if (!IsAllowedOrigin(request.RequestUri))
            {
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }

            auth = request.Headers.Authorization;
            if (auth != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(auth.Scheme, token);
            }
            else if (token.IndexOf(' ') >= 0 && AuthenticationHeaderValue.TryParse(token, out var parsed))
            {
                request.Headers.Authorization = parsed;
            }
            else
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        bool IsAllowedOrigin(Uri? requestUri) =>
            authorizationHeaderOrigin is null
            || (requestUri is { IsAbsoluteUri: true }
                && authorizationHeaderOrigin.Scheme == requestUri.Scheme
                && string.Equals(authorizationHeaderOrigin.IdnHost, requestUri.IdnHost, StringComparison.OrdinalIgnoreCase)
                && authorizationHeaderOrigin.Port == requestUri.Port);

    }
}
