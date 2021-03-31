using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KdSoft.EtwEvents.AgentManager
{
    public static class Constants
    {
        public const string EventStreamHeaderValue = "text/event-stream";
        public const string CloseEvent = "##close";
        public const string KeepAliveEvent = "##keepAlive";
        public const string GetStateEvent = "GetState";

        public const string X500DistNameClaim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/x500distinguishedname";
    }
}
