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
    lastLine += 1;
  }
  const endLine = lineCount - 1;
  if (lastLine <= endLine) {
    totalLineSpans.push({ template: true, start: lastLine, end: endLine, indent: 0 });
  }
  return totalLineSpans;
}

function splitSourceIntoParts(filterSource) {
  const filterParts = [];
  const lineSpans = getAllLineSpans(filterSource.dynamicLineSpans, filterSource.sourceLines.length);

  for (let indx = 0; indx < lineSpans.length; indx += 1) {
    const lineSpan = lineSpans[indx];
    const partLines = getPartLines(filterSource.sourceLines, lineSpan);
    filterParts.push({
      name: lineSpan.template ? `template${indx}` : `dynamic${indx}`,
      lines: partLines.map(pl => pl.text),
      indent: lineSpan.indent
    });
  }
  return filterParts;
}

const lsrx = /\r\n|\n\r|\n|\r/g;

class FilterEditModel {
  constructor(filterSource, diagnostics) {
    this.diagnostics = diagnostics || [];
    const parts = splitSourceIntoParts(filterSource) || [];
    this.parts = parts;
    this.cleanParts = utils.clone(parts);
  }

  refresh(filterSource) {
    const parts = splitSourceIntoParts(filterSource) || [];
    this.cleanParts = parts;
  }

  clearDynamicParts() {
    for (let indx = 0; indx < this.parts.length; indx += 1) {
      const part = this.parts[indx];
      if (part.name.startsWith('dynamic')) {
        part.lines = [];
      }
    }
  }

  setValue(name, value) {
    const part = this.parts.find(dp => dp.name === name);
    if (part) {
      const lines = (value || '').replace(lsrx, '\n').split('\n');
      part.lines = lines;
    }
  }

  reset() {
    this.diagnostics = [];
    this.parts = utils.clone(this.cleanParts);
  }
}

export default FilterEditModel;
