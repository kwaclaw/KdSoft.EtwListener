using System;
using Google.Protobuf.WellKnownTypes;

namespace KdSoft.EtwLogging
{
    public static class ProtoUtils
    {
        public static readonly long BclSecondsAtUnixEpoch = DateTime.UnixEpoch.Ticks / TimeSpan.TicksPerSecond;

        public static Timestamp TimeStampFromUtcTicks(long ticks) {
            // Do the arithmetic using DateTime.Ticks, which is always non-negative, making things simpler.
            long secondsSinceBclEpoch = ticks / TimeSpan.TicksPerSecond;
            int nanoseconds = (int)(ticks % TimeSpan.TicksPerSecond) * Duration.NanosecondsPerTick;
            return new Timestamp { Seconds = secondsSinceBclEpoch - BclSecondsAtUnixEpoch, Nanos = nanoseconds };
        }
    }
}
