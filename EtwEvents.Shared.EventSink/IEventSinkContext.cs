using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents
{
    public interface IEventSinkContext
    {
        string SiteName { get; }
        ILogger Logger { get; }
    }
}
