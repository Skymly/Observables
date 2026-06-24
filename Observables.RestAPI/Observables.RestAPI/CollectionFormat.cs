namespace Observables.RestAPI
{
    /// <summary>
    /// Collection format defined in https://swagger.io/docs/specification/2-0/describing-parameters/
    /// </summary>
    public enum CollectionFormat
    {
        /// <summary>
        /// Values formatted with <see cref="RestApiSettings.UrlParameterFormatter"/> or
        /// <see cref="RestApiSettings.FormUrlEncodedParameterFormatter"/>.
        /// </summary>
        Default,

        /// <summary>
        /// Comma-separated values
        /// </summary>
        Csv,

        /// <summary>
        /// Space-separated values
        /// </summary>
        Ssv,

        /// <summary>
        /// Tab-separated values
        /// </summary>
        Tsv,

        /// <summary>
        /// Pipe-separated values
        /// </summary>
        Pipes,

        /// <summary>
        /// Multiple parameter instances
        /// </summary>
        Multi
    }
}
