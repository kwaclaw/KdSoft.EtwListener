using System.Collections.Generic;
using KdSoft.EtwEvents.AgentManager;
using KdSoft.EtwEvents.Server;
using Xunit;
using Xunit.Abstractions;
using fu = KdSoft.EtwEvents.FilterUtils;

namespace EtwEvents.Tests
{
    public class FilterTests
    {
        readonly ITestOutputHelper _output;

        public FilterTests(ITestOutputHelper output) {
            this._output = output;
        }

        [Fact]
        public void Test1() {
            var codeParts = new string[] {
                "using System;",
                "int _count; tttt",
                "_count = 23;\ncount++;",
                "if (_count > 0)\n    return true;\nelse\n    return false;"
            };
            var filter = Filter.MergeFilterTemplate(codeParts);
            var (sourceText, ranges) = fu.BuildSourceText(filter);

            //NOTE: it is possible that diagnostics are reported against lines in the template,
            //      depending on the nature of the error in the replaceable part of the code
            var diagnostics = RealTimeTraceSession.TestFilter(sourceText);

            _output.WriteLine("---- Part Spans (line:character -- line:character excl.):");
            var linePositions = fu.GetPartLineSpans(sourceText!, ranges!);
            int partIndx = 0;
            foreach (var linePos in linePositions) {
                _output.WriteLine($"Part{++partIndx}: {linePos.Start.Line}:{linePos.Start.Character} -- {linePos.End.Line}:{linePos.End.Character}");
            }
            _output.WriteLine("");

            _output.WriteLine("---- Diagnostics:");
            foreach (var dg in diagnostics) {
                _output.WriteLine(dg.ToString());
            }
            _output.WriteLine("");

            _output.WriteLine("---- Source:");
            List<KdSoft.EtwLogging.TextLine> textLines = new List<KdSoft.EtwLogging.TextLine>();
            foreach (var line in sourceText!.Lines) {
                var textLine = new KdSoft.EtwLogging.TextLine {
                    Line = line.LineNumber,
                    Text = line.ToString(),
                };
                textLines.Add(textLine);
            }
            _output.WriteLine(sourceText.ToString());

        }
    }
}
