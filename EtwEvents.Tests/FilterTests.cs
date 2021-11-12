using System.Collections.Generic;
using KdSoft.EtwEvents.PushAgent;
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

        public const string FilterTemplate = @"using System;
using System.Linq;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Extensions.Configuration;
{0}

namespace KdSoft.EtwEvents.Server
{{
    public class EventFilter: IEventFilter
    {{
        readonly IConfiguration _config;

{1}

        public EventFilter(IConfiguration config) {{
            this._config = config;
            Init();
        }}

        void Init() {{
{2}
        }}

        public bool IncludeEvent(TraceEvent evt) {{
{3}
        }}
    }}
}}
";

        const string header = @"using System.Text;";
        const string body = @"int _count;";
        const string init = @"_count = 23;
_count++";
        const string method = @"if (_count > 0)
    return true;
else
    return false;";

        [Fact]
        public void Test1()
        {
            var (sourceText, ranges) = SessionWorker.BuildSource(FilterTemplate, header, body, init, method);

            var linePositions = SessionWorker.GetPartLineSpans(sourceText, ranges);
            foreach (var linePos in linePositions) {
                _output.WriteLine($"Part: {linePos.Start.Line}:{linePos.Start.Character} -- {linePos.End.Line}:{linePos.End.Character}");
            }
            _output.WriteLine("");

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
