namespace Observables.Postgres;

static class PostgresTrimAnnotations
{
    internal const string JsonPayload =
        "JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.";
}
