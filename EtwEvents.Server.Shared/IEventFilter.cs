using Microsoft.Diagnostics.Tracing;

namespace KdSoft.EtwEvents
{
    public interface IEventFilter
    {
        bool IncludeEvent(TraceEvent evt);
    }
}
