using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace KdSoft.EtwLogging
{
    public partial class BuildFilterResult
    {
        public BuildFilterResult(ImmutableArray<Diagnostic> diagnostics) : this() {
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
                this.Diagnostics.Add(dg);
            }
        }
    }
}
