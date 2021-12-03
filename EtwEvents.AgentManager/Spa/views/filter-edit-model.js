import * as utils from '../js/utils.js';

function getPartLines(sourceLines, lineSpan) {
  const result = [];
  if (!lineSpan) return result;

  for (let indx = lineSpan.start; indx <= lineSpan.end; indx += 1) {
    result.push(sourceLines[indx]);
  }
  return result;
}

function getAllLineSpans(partLineSpans, lineCount) {
  const totalLineSpans = [];
  let lastLine = 0;
  // we  work with full lines, character positions are ignored
  for (const partLineSpan of partLineSpans) {
    const startLine = partLineSpan.start.line;
    if (startLine > lastLine) {
      totalLineSpans.push({ template: true, start: lastLine, end: startLine - 1 });
    }
    lastLine = partLineSpan.end.line;
    totalLineSpans.push({ template: false, start: startLine, end: lastLine });
  }
  const endLine = lineCount - 1;
  if (lastLine < endLine) {
    totalLineSpans.push({ template: true, start: lastLine, end: endLine });
  }
  return totalLineSpans;
}

// const filterPart = { name: '', lines: [] }
function splitSourceIntoParts(filterSource) {
  const filterParts = [];
  const lineSpans = getAllLineSpans(filterSource.partLineSpans, filterSource.sourceLines.length);

  for (const lineSpan of lineSpans) {
    const partLines = getPartLines(filterSource.sourceLines, lineSpan);
    filterParts.push({
      name: lineSpan.template ? 'template' : 'dynamic',
      lines: partLines
    });
  }
  return filterParts;
}

const lsrx = /\r\n|\n\r|\n|\r/g;

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
      if (part.name === 'dynamic') {
        newDynParts.push(part);
      }
    }
    this.dynamicParts = newDynParts;
    this.cleanDynamicParts = utils.clone(newDynParts);
  }

  resetDiagnostics() {
    this.diagnostics = [];
  }

  _getPartText(indx) {
    const lines = this.dynamicParts[indx]?.lines;
    return lines?.join('\n') || null;
  }

  _setPartText(indx, value) {
    // normalize line-breaks for splitting
    const lines = value.replace(lsrx, '\n').split('\n');
    this.dynamicParts[indx].lines = lines;
    this.diagnostics = [];
  }

  get header() { return this._getPartText(0); }
  set header(value) { this._setPartText(0, value); }

  get body() { return this._getPartText(1); }
  set body(value) { this._setPartText(1, value); }

  get init() { return this._getPartText(2); }
  set init(value) { this._setPartText(2, value);  }

  get method() { return this._getPartText(3); }
  set method(value) { this._setPartText(3, value); }

  get isModified() {
    return utils.targetEquals(this.cleanDynamicParts, this.dynamicParts);
  }
}

export default FilterEditModel;
