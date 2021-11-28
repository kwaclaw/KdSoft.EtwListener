using System.Collections.Generic;
using KdSoft.EtwEvents.AgentManager.Services;
using KdSoft.EtwEvents.PushAgent;
using KdSoft.EtwEvents.Server;
using KdSoft.EtwLogging;
using Xunit;
using Xunit.Abstractions;

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
            var parts = new FilterPart[] {
                new FilterPart { Name = "header" },
                new FilterPart { Name = "body", Lines = { @"int _count; tttt" } },
                new FilterPart { Name = "init", Lines = { @"_count = 23;", @"_count++;" } },
                new FilterPart { Name = "method", Lines = { @"if (_count > 0)", @"    return true;", "else", "    return false;" } },
            };
            var filter = FilterHelper.MergeFilterTemplate(parts);
            var (sourceText, ranges) = SessionWorker.BuildSourceText(filter);

            //NOTE: it is possible that diagnostics are reported against lines in the template,
            //      depending on the nature of the error in the replaceable part of the code
            var diagnostics = RealTimeTraceSession.TestFilter(sourceText);

            _output.WriteLine("---- Part Spans (line:character -- line:character excl.):");
            var linePositions = SessionWorker.GetPartLineSpans(sourceText, ranges);
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
            foreach (var line in sourceText.Lines) {
                var textLine = new KdSoft.EtwLogging.TextLine {
                    Line = line.LineNumber,
                    Start = line.Start,
                    Length = line.Span.Length,
                    Text = line.ToString(),
                };
                textLines.Add(textLine);
                _output.WriteLine(textLine.Text);
            }


            //int rr = 29;
        }
    }
}
