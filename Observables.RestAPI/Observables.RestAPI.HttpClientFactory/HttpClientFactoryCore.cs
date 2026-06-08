using System;
using System.Net.Http;
using Observables.RestAPI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Observables.RestAPI.HttpClientFactory;

internal static class HttpClientFactoryCore
{
    internal static IHttpClientBuilder AddRestApiClientCore<T>(
        IServiceCollection services,
        Action<HttpClient>? configureClient,
        Func<IServiceProvider, RestApiSettings?>? settings)
        where T : class
    {
        services.AddSingleton(provider => new SettingsFor<T>(settings?.Invoke(provider)));

        services.AddSingleton<IRequestBuilder<T>>(provider =>
            RequestBuilder.ForType<T>(provider.GetRequiredService<SettingsFor<T>>().Settings));

        var builder = services.AddHttpClient(typeof(T).FullName ?? typeof(T).Name, configureClient ?? (_ => { }));

        builder.ConfigurePrimaryHttpMessageHandler(sp =>
            CreateInnerHandlerIfProvided(sp.GetRequiredService<SettingsFor<T>>().Settings)
                ?? new HttpClientHandler());

        return builder.AddTypedClient((client, serviceProvider) =>
            RestService.For<T>(client, serviceProvider.GetRequiredService<IRequestBuilder<T>>()));
    }

    static HttpMessageHandler? CreateInnerHandlerIfProvided(RestApiSettings? settings)
    {
        HttpMessageHandler? innerHandler = null;
        if (settings is null)
        {
            return null;
        }

        if (settings.HttpMessageHandlerFactory is not null)
        {
            innerHandler = settings.HttpMessageHandlerFactory();
        }

        if (settings.AuthorizationHeaderValueGetter is not null)
        {
            innerHandler = new AuthenticatedHttpClientHandler(
                settings.AuthorizationHeaderValueGetter,
                innerHandler);
        }

        return innerHandler;
    }
}
