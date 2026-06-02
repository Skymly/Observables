using Observables.RestAPI;

namespace Observables.RestAPI.HttpClientFactory;

internal interface ISettingsFor
{
    RestApiSettings? Settings { get; }
}

internal sealed class SettingsFor<T>(RestApiSettings? settings) : ISettingsFor
{
    public RestApiSettings? Settings { get; } = settings;
}
