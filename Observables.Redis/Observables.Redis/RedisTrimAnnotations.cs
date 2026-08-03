#if NET8_0_OR_GREATER
namespace Observables.Redis;

static class RedisTrimAnnotations
{
    internal const string JsonPayload =
        "JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.";
}
#endif
