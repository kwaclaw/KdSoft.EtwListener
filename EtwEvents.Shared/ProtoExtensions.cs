using System.Collections.Immutable;
using System.Diagnostics;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Diagnostics.Tracing;
using ca = Microsoft.CodeAnalysis;
using cat = Microsoft.CodeAnalysis.Text;

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

        public static BuildFilterResult AddDiagnostics(this BuildFilterResult bfr, ImmutableArray<ca.Diagnostic> diagnostics) {
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

        public static FilterSource AddSourceLines(this FilterSource filterSource, IReadOnlyList<cat.TextLine> lines) {
            foreach (var line in lines) {
                var textLine = new TextLine {
                    Line = line.LineNumber,
                    Text = line.ToString(),
                };
                filterSource.SourceLines.Add(textLine);
            }
            return filterSource;
        }

        public static FilterSource AddDynamicLineSpans(
            this FilterSource filterSource,
            IReadOnlyList<cat.LinePositionSpan> linePositionSpans,
            IList<FilterPart> dynamicParts
        ) {
            Debug.Assert(linePositionSpans.Count == dynamicParts.Count);
            for (int indx = 0; indx < linePositionSpans.Count; indx++) {
                var linePositionSpan = linePositionSpans[indx];
                var lineSpan = new LineSpan {
                    Start = linePositionSpan.Start.Line,
                    End = linePositionSpan.End.Line,
                    Indent = dynamicParts[indx].Indent
                };
                filterSource.DynamicLineSpans.Add(lineSpan);
            }
            return filterSource;
        }
    }
}
