using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Observables.RestAPI;

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

        var builder = services.AddHttpClient(typeof(T).FullName ?? typeof(T).Name, configureClient ?? (_ => { }));

        builder.ConfigurePrimaryHttpMessageHandler(sp =>
            RestService.CreateHttpMessageHandler(sp.GetRequiredService<SettingsFor<T>>().Settings));

        return builder.AddTypedClient((client, serviceProvider) =>
            RestService.For<T>(client, serviceProvider.GetRequiredService<SettingsFor<T>>().Settings));
    }

}
