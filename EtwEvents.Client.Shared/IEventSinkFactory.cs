using System.Threading.Tasks;

namespace KdSoft.EtwEvents.Client.Shared
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
        /// <param name="name"></param>
        /// <param name="optionsJson"></param>
        /// <param name="credentialsJson"></param>
        /// <returns></returns>
        Task<IEventSink> Create(string name, string optionsJson, string credentialsJson);
    }
}
