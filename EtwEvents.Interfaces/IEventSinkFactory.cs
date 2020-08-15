using System.Threading.Tasks;

namespace KdSoft.EtwEvents.Client.Shared
{
    public interface IEventSinkFactory
    {
        string GetOptionsJsonSchema();
        string GetCredentialsJsonSchema();

        Task<IEventSink> Create(string name, string optionsJson, string credentialsJson);
    }
}
