#if NET8_0_OR_GREATER
namespace Observables.Mqtt;

static class MqttTrimAnnotations
{
    internal const string JsonPayload =
        "JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.";
}
#endif
