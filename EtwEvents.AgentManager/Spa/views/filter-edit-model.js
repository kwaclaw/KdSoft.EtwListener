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
      lines: partLines.map(pl => pl.text),
      indent: lineSpan.indent
    });
  }
  return filterParts;
}

function getParts(filterSource) {
  const parts = filterSource ? splitSourceIntoParts(filterSource) : [];
  const dynParts = [];
  for (let indx = 0; indx < parts.length; indx += 1) {
    const part = parts[indx];
    if (part.name.startsWith('dynamic')) {
      dynParts.push(part);
    }
  }
  return [parts, dynParts];
}

const lsrx = /\r\n|\n\r|\n|\r/g;

class FilterEditModel {
  constructor(filterSource, diagnostics) {
    this.diagnostics = diagnostics || [];
    const [parts, dynParts] = getParts(filterSource);
    this.filterParts = parts;
    this.cleanFilterParts = utils.clone(parts);
    this.dynamicParts = dynParts;
    this.cleanDynamicParts = utils.clone(dynParts);
  }

  refresh(filterSource) {
    const [parts, dynParts] = getParts(filterSource);
    this.cleanFilterParts = parts;
    this.cleanDynamicParts = dynParts;
  }

  setValue(name, value) {
    const part = this.dynamicParts.find(dp => dp.name === name);
    if (part) {
      const lines = (value || '').replace(lsrx, '\n').split('\n');
      part.lines = lines;
    }
  }

  getValue(name) {
    const part = this.dynamicParts.find(dp => dp.name === name);
    if (part) {
      return part.lines?.join('\n');
    }
    return null;
  }

  resetDiagnostics() {
    this.diagnostics = [];
  }
}

export default FilterEditModel;
