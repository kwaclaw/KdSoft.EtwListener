import * as utils from '../js/utils.js';

function getPartLines(sourceLines, lineSpan) {
  const result = [];
  if (!lineSpan) return result;

  for (let indx = lineSpan.start; indx <= lineSpan.end; indx += 1) {
    result.push(sourceLines[indx]);
  }
  return result;
}

function getAllLineSpans(dynamicLineSpans, lineCount) {
  const totalLineSpans = [];
  let lastLine = 0;
  // we  work with full lines, character positions are ignored
  for (const dynamicLineSpan of dynamicLineSpans) {
    const startLine = dynamicLineSpan.start.line;
    if (startLine > lastLine) {
      totalLineSpans.push({ template: true, start: lastLine, end: startLine - 1, indent: 0 });
    }
    lastLine = dynamicLineSpan.end.line;
    totalLineSpans.push({ template: false, start: startLine, end: lastLine, indent: dynamicLineSpan.indent });
  }
  const endLine = lineCount - 1;
  if (lastLine < endLine) {
    totalLineSpans.push({ template: true, start: lastLine, end: endLine, indent: 0 });
  }
  return totalLineSpans;
}

// const filterPart = { name: '', lines: [] }
function splitSourceIntoParts(filterSource) {
  const filterParts = [];
  const lineSpans = getAllLineSpans(filterSource.dynamicLineSpans, filterSource.sourceLines.length);

  for (let indx = 0; indx < lineSpans.length; indx += 1) {
    const lineSpan = lineSpans[indx];
    const partLines = getPartLines(filterSource.sourceLines, lineSpan);
    filterParts.push({
      name: lineSpan.template ? `template${indx}` : `dynamic${indx}`,
      lines: partLines,
      indent: lineSpan.indent
    });
  }
  return filterParts;
}

// const lsrx = /\r\n|\n\r|\n|\r/g;

class FilterEditModel {
  constructor(filterSource, diagnostics) {
    this.diagnostics = diagnostics || [];
    this.refresh(filterSource);
  }

  refresh(filterSource) {
    this.filterParts = filterSource ? splitSourceIntoParts(filterSource) : [];
    const newDynParts = [];
    for (let indx = 0; indx < this.filterParts.length; indx += 1) {
      const part = this.filterParts[indx];
      if (part.name.startsWith('dynamic')) {
        newDynParts.push(part);
      }
    }
    this.dynamicParts = newDynParts;
    this.cleanDynamicParts = utils.clone(newDynParts);
  }

  resetDiagnostics() {
    this.diagnostics = [];
  }
}

export default FilterEditModel;
