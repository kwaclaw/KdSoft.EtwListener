import { html } from '../lib/lit-html.js';
import { LitMvvmElement } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { css } from '../styles/css-tag.js';
import sharedStyles from '../styles/kdsoft-shared-styles.js';

function comparePos(pos1, pos2) {
  if (pos1.line < pos2.line) return -1;
  if (pos1.line > pos2.line) return 1;
  if (pos1.character < pos2.character) return -1;
  if (pos1.character > pos2.character) return 1;
  return 0;
}

function getSpan(dg) {
  return {
    start: dg.lineSpan.start,
    end: dg.lineSpan.end,
  };
}

function getOverlap(span1, span2) {
  if (comparePos(span1.start, span2.end) > 0) return null;
  if (comparePos(span1.end, span2.start) < 0) return null;

  // we have overlap, calculate the merged span
  const result = {};

  if (comparePos(span1.start, span2.start) < 0) {
    result.start = span1.start;
  } else {
    result.start = span2.start;
  }

  if (comparePos(span1.end, span2.end) > 0) {
    result.end = span1.end;
  } else {
    result.end = span2.end;
  }

  return result;
}

function mergeOverlapping(dgSpans, startIndex) {
  if (typeof startIndex === 'undefined') {
    startIndex = dgSpans.length - 1;
  }
  if (startIndex <= 0) {
    return;
  }

  const baseDgSpan = dgSpans[startIndex];
  for (let dgi = startIndex - 1; dgi >= 0; dgi -= 1) {
    const dgSpan = dgSpans[dgi];
    const overlap = getOverlap(baseDgSpan.span, dgSpan.span);
    if (overlap) {
      dgSpan.span = overlap;
      dgSpan.diagnostics = dgSpan.diagnostics.concat(baseDgSpan.diagnostics);
      dgSpans[startIndex] = null;
      break;
    }
  }
  mergeOverlapping(dgSpans, startIndex - 1);
}

function consolidateOverlappingSpans(diagnostics) {
  let result = [];

  for (let indx = 0; indx < diagnostics.length; indx += 1) {
    const dg = diagnostics[indx];
    result.push({ span: getSpan(dg), diagnostics: [dg] });
  }

  mergeOverlapping(result);
  // remove null entries
  result = result.filter(r => !!r);

  // since they don't overlap we can sort simply by span start positions
  result.sort((x, y) => comparePos(x.span.start, y.span.start));

  // console.log(JSON.stringify(diagnostics));
  // console.log(JSON.stringify(result));
  return result;
}

const lineOffset = 9;

// end.line is and inclusive boundary, end.character is an inclusive boundary
function getStringSegmentForSpan(lines, start, end) {
  const startLineIndex = start.line - lineOffset;
  const endLineIndex = end.line - lineOffset;

  const segments = [];
  for (let li = startLineIndex; li <= endLineIndex; li += 1) {
    const line = lines[li];
    if (!line) continue; // ignore undefined entries
    const startChar = li === startLineIndex ? start.character : 0;
    const endChar = li === endLineIndex ? end.character : line.length - 1;
    segments.push(line.slice(startChar, endChar - startChar + 1));
  }

  return segments.join('\n');
}

const lsrx = /\r\n|\n\r|\n|\r/g;

function getToolTip(diagnostics) {
  const dglines = diagnostics.map(dg => `${dg.id} : ${dg.message}`);
  return dglines.join('\n');
}

// assumes that diagMap entries are sorted by position
function correctSpansForExistingLines(diagMap, lines) {
  const result = [];
  const lastLineIndex = lineOffset + lines.length - 1;

  for (let diagIndx = 0; diagIndx < diagMap.length; diagIndx += 1) {
    const dgEntry = diagMap[diagIndx];
    const span = dgEntry.span;

    // skip entries with line-numbers outside of our code lines
    if (span.end.line < lineOffset) continue;
    if (span.start.line > lastLineIndex) {
      break;
    }

    // fix partial spans
    if (span.start.line < lineOffset && span.end.line >= lineOffset) {
      span.start.line = lineOffset;
      span.start.character = 0;
    } else if (span.start.line <= lastLineIndex && span.end.line > lastLineIndex) {
      span.end.line = lastLineIndex;
      span.end.character = lines[lastLineIndex].length - 1;
    }

    result.push(dgEntry);
  }
  return result;
}

// returns a raw string with markup inserted, *not* a TemplateResult,
// because we have to manage a contenteditable div outside of lit-html
function formatFilter(filter, diagnostics) {
  if (!diagnostics || diagnostics.length === 0) return filter;

  // normalize line-breaks for splitting
  const lines = filter.replace(lsrx, '\n').split('\n');

  let diagMap = consolidateOverlappingSpans(diagnostics);
  diagMap = correctSpansForExistingLines(diagMap, lines);

  const markupSpans = [];
  let nextPosition = { line: lineOffset, character: 0 };
  let nextNewLine = '';

  for (let diagIndx = 0; diagIndx < diagMap.length; diagIndx += 1) {
    const dgEntry = diagMap[diagIndx];
    const span = dgEntry.span;

    // collect text before this diagnostic span
    let beforeSpanStartPosition;
    const beforeStartLine = lines[span.start.line - 1 - lineOffset];
    if (span.start.character === 0) { // need to go to end of preceding line, which might not exist
      beforeSpanStartPosition = { line: span.start.line - 1, character: (beforeStartLine || []).length - 1 };
    } else {
      beforeSpanStartPosition = { line: span.start.line, character: span.start.character - 1 };
    }
    const beforeStr = getStringSegmentForSpan(lines, nextPosition, beforeSpanStartPosition);

    // check if we need a line-break before this span
    const beforeNewLine = !!beforeStartLine && span.start.character === 0 ? '\n' : '';

    const spanStr = getStringSegmentForSpan(lines, span.start, span.end);
    const tooltip = getToolTip(dgEntry.diagnostics);
    markupSpans.push(`${nextNewLine}${beforeStr}${beforeNewLine}<mark title="${tooltip}">${spanStr}</mark>`);

    // calculate start position for next segment/span
    const spanEndLine = lines[span.end.line - lineOffset];
    if (span.end.character >= spanEndLine.length - 1) { // we consumed the whole line
      nextPosition = { line: span.end.line + 1, character: 0 };
      nextNewLine = '\n';
    } else {
      nextPosition = { line: span.end.line, character: span.end.character + 1 };
      nextNewLine = '';
    }
  }

  // deal with remaining lines and segments
  const nextLine = lines[nextPosition.line - lineOffset];
  if (nextLine) {
    const lastLine = lines[lines.length - 1];
    const endPosition = { line: lineOffset + lines.length - 1, character: lastLine.length - 1 };
    const postStr = getStringSegmentForSpan(lines, nextPosition, endPosition);
    markupSpans.push(`${nextNewLine}${postStr}`);
  }

  return markupSpans.join('');
}


class FilterEdit extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.LOW);
  }

  _change(e) {
    this.model.diagnostics = [];
    this.model.filter = e.currentTarget.innerText;
  }

  static get styles() {
    return [
      css`
        #code-wrapper {
          color: gray;
          line-height: 1rem;
        }
        #code {
          display: inline-block;
          margin-left: auto;
          border: 1px solid LightGray;
          color: initial;
          padding: 3px;
          line-height: 1.5rem !important;
          font: inherit;
          resize: both;
          white-space: pre;
          overflow-wrap: normal;
          width: 68ch;
        }
        /* only needed for contenteditable elements */
        #code:empty::after {
          color: gray;
          content: attr(placeholder);
        }
        #code.invalid {
          border: 1px solid red;
        }
        mark {
          background-color: transparent;
          text-decoration: red wavy underline;
          text-underline-position: auto;
        }
      `,
    ];
  }

  //TODO intercept tab key in code div

  rendered() {
    const filter = this.model.filter;
    const formattedFilter = formatFilter(filter, this.model.diagnostics);
    const codeElement = this.renderRoot.getElementById('code');
    codeElement.innerHTML = formattedFilter;
    codeElement.classList.toggle('invalid', this.model.diagnostics.length);
  }

  render() {
    const codeToolTip = getToolTip(this.model.diagnostics);
    const result = html`
      ${sharedStyles}
      <style>
        :host {
          display: block;
        }
      </style>
      <div id="code-wrapper" class="border p-2"><pre>${html`using System;
using Microsoft.Diagnostics.Tracing;

namespace EtwEvents.Server
{
  public class EventFilter: IEventFilter
  {
    public bool IncludeEvent(TraceEvent evt) {
      ${html`<div id="code"
        contenteditable="true"
        @blur=${this._change}
        spellcheck="false"
        title=${codeToolTip}
        placeholder="Your code goes here"></div>`}
    }
  }
}`}   </pre></div>
    `;
    return result;
  }
}

window.customElements.define('filter-edit', FilterEdit);
