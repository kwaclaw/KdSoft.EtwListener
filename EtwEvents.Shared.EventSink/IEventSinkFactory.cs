namespace KdSoft.EtwEvents
{
    public interface IEventSinkFactory
    {
        /// <summary>
        /// Returns JsonSchema for event sink options.
        /// </summary>
        string GetOptionsJsonSchema();

        /// <summary>
        /// Returns JsonSchema for credentials.
        /// </summary>
        string GetCredentialsJsonSchema();

        /// <summary>
        /// Creates new instance of IEventSink.
        /// </summary>
        /// <param name="optionsJson">Options encoded in JSON.</param>
        /// <param name="credentialsJson">Credentials encoded in JSON.</param>
        /// <param name="context">Provides execution context for event sink.</param>
        /// <returns></returns>
        Task<IEventSink> Create(string optionsJson, string credentialsJson, IEventSinkContext context);
    }
}
