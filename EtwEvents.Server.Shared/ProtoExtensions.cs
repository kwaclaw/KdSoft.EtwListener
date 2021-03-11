using System.Collections.Immutable;
using Google.Protobuf.WellKnownTypes;
using Microsoft.CodeAnalysis;
using Microsoft.Diagnostics.Tracing;

namespace KdSoft.EtwLogging
{
    public static class ProtoExtensions
    {
        // we ignore TraceEvent.FormattedMessage, as it just contains a formatted representation of the Payload
        public static EtwEvent SetTraceEvent(this EtwEvent etw, TraceEvent evt) {
            etw.ProviderName = evt.ProviderName;
            etw.Channel = (uint)evt.Channel;
            etw.Id = (uint)evt.ID;
            etw.Keywords = (long)evt.Keywords;
            etw.Level = (TraceEventLevel)evt.Level;
            etw.Opcode = (uint)evt.Opcode;
            etw.OpcodeName = evt.OpcodeName;
            etw.TaskName = evt.TaskName;
            etw.TimeStamp = evt.TimeStamp.ToUniversalTime().ToTimestamp();
            etw.Version = evt.Version;
            for (int indx = 0; indx < evt.PayloadNames.Length; indx++) {
                var propName = evt.PayloadNames[indx];
                etw.Payload[propName] = evt.PayloadString(indx);
            }
            return etw;
        }

        public static BuildFilterResult AddDiagnostics(this BuildFilterResult bfr, ImmutableArray<Diagnostic> diagnostics) {
            foreach (var diag in diagnostics) {
                LinePositionSpan? lineSpan = null;
                if (diag.Location.IsInSource) {
                    var ls = diag.Location.GetLineSpan();
                    lineSpan = new LinePositionSpan {
                        Start = new LinePosition { Line = ls.StartLinePosition.Line, Character = ls.StartLinePosition.Character },
                        End = new LinePosition { Line = ls.EndLinePosition.Line, Character = ls.EndLinePosition.Character }
                    };
                }
                var dg = new CompileDiagnostic {
                    Id = diag.Id,
                    IsWarningAsError = diag.IsWarningAsError,
                    WarningLevel = diag.WarningLevel,
                    Severity = (CompileDiagnosticSeverity)diag.Severity,
                    Message = diag.GetMessage(),
                    LineSpan = lineSpan
                };
                bfr.Diagnostics.Add(dg);
            }
            return bfr;
        }
    }
}
