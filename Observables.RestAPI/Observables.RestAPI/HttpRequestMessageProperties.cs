namespace Observables.RestAPI
{
    /// <summary>
    /// Contains Observables.RestAPI-defined properties on HttpRequestMessage.Properties/Options.
    /// </summary>
    public static class HttpRequestMessageOptions
    {
        /// <summary>
        /// Returns the <see cref="System.Type"/> of the top-level interface where the method was called from
        /// </summary>
        public static string InterfaceType { get; } = "Refit.InterfaceType";
    }
}
