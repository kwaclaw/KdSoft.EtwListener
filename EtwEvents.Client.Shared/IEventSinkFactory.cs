using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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
        /// <param name="optionsJson"></param>
        /// <param name="credentialsJson"></param>
        /// <returns></returns>
        Task<IEventSink> Create(string optionsJson, string credentialsJson, ILogger logger);
    }
}
