#if NET5_0_OR_GREATER
namespace Observables.RestAPI;

static class RestTrimAnnotations
{
    internal const string Reflection =
        "RestAPI uses reflection on interface methods and DTO types. Preserve required members when trimming.";

    internal const string Dynamic =
        "RestAPI uses MakeGenericMethod and reflection at runtime.";
}
#endif
