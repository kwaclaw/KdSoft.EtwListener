﻿using System.Diagnostics;
using System.Text;
using KdSoft.EtwLogging;
using Microsoft.CodeAnalysis;
using Mcat = Microsoft.CodeAnalysis.Text;

namespace KdSoft.EtwEvents
{
    public static class FilterUtils
    {
        public static (string? source, List<string> markers) BuildTemplateSource(Filter filter) {
            var sb = new StringBuilder();
            var markers = new List<string>();
            if (filter.FilterParts.Count == 0) {
                return (null, markers);
            }
            int indx = 0;
            foreach (var filterPart in filter.FilterParts) {
                var partName = filterPart.Name?.Trim();
                if (string.IsNullOrEmpty(partName)) {
                    return (null, markers);
                }
                if (partName.StartsWith("template", StringComparison.OrdinalIgnoreCase)) {
                    sb.Append(filterPart.Code);
                }
                else {
                    var marker = $"\u001D{indx++}";
                    sb.Append(marker);
                    markers.Add(marker);
                }
            }

            var source = sb.ToString();
            if (string.IsNullOrWhiteSpace(source)) {
                source = null;
            }
            return (source, markers);
        }

        public static List<Mcat.TextChange> BuildSourceChanges(string initSource, IList<string> markers, Filter filter) {
            var sb = new StringBuilder();
            int indx = 0;
            var partChanges = new List<Mcat.TextChange>(markers.Count);
            foreach (var filterPart in filter.FilterParts) {
                var partName = filterPart.Name?.Trim();
                if (string.IsNullOrEmpty(partName) || partName.StartsWith("template", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                int insertionIndex = initSource.IndexOf(markers[indx++], StringComparison.Ordinal);
                sb.Clear();

                partChanges.Add(new Mcat.TextChange(new Mcat.TextSpan(insertionIndex, 2), filterPart.Code));
            }
            return partChanges;
        }

        public static (Mcat.SourceText? sourceText, IReadOnlyList<Mcat.TextChangeRange>? dynamicRanges) BuildSourceText(Filter filter) {
            (Mcat.SourceText? sourceText, IReadOnlyList<Mcat.TextChangeRange>? dynamicRanges) result;

            var (templateSource, markers) = BuildTemplateSource(filter);
            if (templateSource == null)
                return (null, null);

            var partChanges = BuildSourceChanges(templateSource, markers, filter);
            Debug.Assert(partChanges.Count == markers.Count);
            if (partChanges.Count == 0)
                return (null, null);

            var initSourceText = Mcat.SourceText.From(templateSource);
            result.sourceText = initSourceText.WithChanges(partChanges);
            result.dynamicRanges = result.sourceText.GetChangeRanges(initSourceText);

            return result;
        }

        public static IReadOnlyList<Mcat.LinePositionSpan> GetPartLineSpans(Mcat.SourceText sourceText, IReadOnlyList<Mcat.TextChangeRange> dynamicRanges) {
            var result = new Mcat.LinePositionSpan[dynamicRanges.Count];
            int offset = 0;
            var lines = sourceText.Lines;
            for (int indx = 0; indx < dynamicRanges.Count; indx++) {
                var newLen = dynamicRanges[indx].NewLength;
                var span = new Mcat.TextSpan(dynamicRanges[indx].Span.Start + offset, newLen);
                result[indx] = lines.GetLinePositionSpan(span);

                offset += newLen - 2;
            }
            return result;
        }

        public static FilterSource BuildFilterSource(Mcat.SourceText sourceText, IReadOnlyList<Mcat.TextChangeRange> dynamicRanges, Filter filter) {
            var dynamicLineSpans = GetPartLineSpans(sourceText, dynamicRanges!);
            var dynamicParts = filter.FilterParts.Where(fp => fp.Name.StartsWith("dynamic", StringComparison.OrdinalIgnoreCase)).ToList();
            return new FilterSource { TemplateVersion = filter.TemplateVersion }
                .AddSourceLines(sourceText.Lines)
                .AddDynamicLineSpans(dynamicLineSpans, dynamicParts);
        }

        public static FilterSource? BuildFilterSource(Filter filter) {
            var (sourceText, dynamicRanges) = BuildSourceText(filter);
            if (sourceText == null)
                return null;

            var dynamicLineSpans = GetPartLineSpans(sourceText, dynamicRanges!);
            var dynamicParts = filter.FilterParts.Where(fp => fp.Name.StartsWith("dynamic", StringComparison.OrdinalIgnoreCase)).ToList();
            return new FilterSource { TemplateVersion = filter.TemplateVersion }
                .AddSourceLines(sourceText.Lines)
                .AddDynamicLineSpans(dynamicLineSpans, dynamicParts);
        }
    }
}
