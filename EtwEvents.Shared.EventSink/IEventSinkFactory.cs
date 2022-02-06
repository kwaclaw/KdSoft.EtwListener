using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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
        /// <returns></returns>
        Task<IEventSink> Create(string optionsJson, string credentialsJson, ILogger logger);
    }
}
