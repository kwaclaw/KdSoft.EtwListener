using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace KdSoft.EtwEvents.Client.Shared
{
    public class EventSinkComparer: IEqualityComparer<IEventSink>
    {
        public static EventSinkComparer Default { get; } = new EventSinkComparer();

        public bool Equals([AllowNull] IEventSink x, [AllowNull] IEventSink y) {
            if (object.ReferenceEquals(x, y))
                return true;
            if (x == null || y == null)
                return false;
            return StringComparer.Ordinal.Equals(x.Name, y.Name);
        }

        public int GetHashCode([DisallowNull] IEventSink obj) {
            return obj.Name?.GetHashCode(StringComparison.Ordinal) ?? 0;
        }
    }
}
