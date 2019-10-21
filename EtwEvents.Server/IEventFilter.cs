using System;
using Microsoft.Diagnostics.Tracing;

namespace EtwEvents
{
    public interface IEventFilter
    {
        bool IncludeEvent(TraceEvent evt);
    }
}
