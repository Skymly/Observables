using System;
using System.Net.Http;
using Observables.RestAPI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Observables.RestAPI.HttpClientFactory;

/// <summary>
/// DI extensions for Observables.RestAPI typed HTTP clients.
/// </summary>
public static class HttpClientFactoryExtensions
{
    /// <summary>
    /// Adds a Observables.RestAPI client to the service collection.
    /// </summary>
    public static IHttpClientBuilder AddRestApiClient<T>(
        this IServiceCollection services,
        Action<HttpClient>? configureClient = null,
        RestApiSettings? settings = null)
        where T : class
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        return HttpClientFactoryCore.AddRestApiClientCore<T>(services, configureClient, _ => settings);
    }

    /// <summary>
    /// Adds a Observables.RestAPI client with settings resolved from DI.
    /// </summary>
    public static IHttpClientBuilder AddRestApiClient<T>(
        this IServiceCollection services,
        Action<HttpClient>? configureClient,
        Func<IServiceProvider, RestApiSettings?>? settingsAction)
        where T : class
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        return HttpClientFactoryCore.AddRestApiClientCore<T>(services, configureClient, settingsAction);
    }
}
