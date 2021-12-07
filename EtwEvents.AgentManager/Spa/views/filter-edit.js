import { html } from 'lit';
import { repeat } from 'lit/directives/repeat.js';
import { LitMvvmElement, css } from '@kdsoft/lit-mvvm';
import { Queue, priorities } from '@nx-js/queue-util/dist/es.es6.js';
import tailwindStyles from '@kdsoft/lit-mvvm-components/styles/tailwind-styles.js';
import fontAwesomeStyles from '@kdsoft/lit-mvvm-components/styles/fontawesome/css/all-styles.js';

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

function getToolTip(diagnostics) {
  const dglines = diagnostics.map(dg => `${dg.id} : ${dg.message}`);
  return dglines.join('\n');
}

function getMarkupsForLine(line, diagMap) {
  const result = [];
  for (const diagEntry of diagMap) {
    if (diagEntry.span.start.line === line.line) {
      const tooltip = getToolTip(diagEntry.diagnostics);
      const markup = `<mark title="${tooltip}">`;
      result.push({ pos: diagEntry.span.start.character, markup });
    }
    if (diagEntry.span.end.line === line.line) {
      result.push({ pos: diagEntry.span.end.character, markup: `</mark>` });
    }
  }
  return result;
}

function getPartLines(sourceLines, partLineSpan) {
  const result = [];
  if (!partLineSpan) return result;

  for (let indx = partLineSpan.start.line; indx <= partLineSpan.end.line; indx += 1) {
    result.push(sourceLines[indx]);
  }
  return result;
}

function getPartMap(partLines, diagMap) {
  if (!diagMap.length) return diagMap;
  if (!partLines.length) return [];

  const startLine = partLines[0].line;
  const endLine = partLines[partLines.length - 1].line;
  const result = [];

  // a span can cross lines within a part, but not parts
  for (let diagIndx = 0; diagIndx < diagMap.length; diagIndx += 1) {
    const dgEntry = diagMap[diagIndx];
    const dgSpan = dgEntry.span;
    if (dgSpan.end.line < startLine) continue;
    if (dgSpan.start.line > endLine) continue;
    result.push(dgEntry);
    delete diagMap[diagIndx];
  }
  return result;
}

function formatPart(lines, diagMap) {
  if (!diagMap?.length) return lines.map(l => l.text).join('\n');

  for (let lineIndx = 0; lineIndx < lines.length; lineIndx += 1) {
    const line = lines[lineIndx];
    const lineParts = [];
    const markups = getMarkupsForLine(line, diagMap);
    let sliceStart = 0;
    const lineText = line.text || '';
    for (const mk of markups) {
      lineParts.push(lineText.slice(sliceStart, mk.pos));
      lineParts.push(mk.markup);
      sliceStart = mk.pos;
    }
    lineParts.push(lineText.slice(sliceStart));
    line.text = lineParts.join('');
  }

  return lines.map(l => l.text).join('\n');
}

class FilterEdit extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.LOW);
  }

  _change(e) {
    //e.stopPropagation();
    this.model.diagnostics = [];
    const name = e.currentTarget.id;
    const value = e.currentTarget.innerText?.trimEnd();
    this.model.setValue(name, value);
  }

  formatPartElement(id, sourceLines, diagMap) {
    const codeElement = this.renderRoot.getElementById(id);
    if (!diagMap.length) {
      codeElement.classList.remove('invalid');
      return;
    }

    codeElement.classList.add('invalid');

    const formattedPart = formatPart(sourceLines, diagMap);
    codeElement.innerHTML = formattedPart;

    const partDiagnostics = diagMap.map(dg => dg.diagnostics).flat(2);
    codeElement.title = getToolTip(partDiagnostics);
  }

  static get styles() {
    return [
      tailwindStyles,
      fontAwesomeStyles,
      css`
        :host {
          display: block;
        }
        #code-wrapper {
          color: gray;
          line-height: 1rem;
          position: relative;
        }
        .code {
          display: inline-block;
          margin-left: auto;
          border: 1px solid LightGray;
          color: initial;
          padding: 3px;
          line-height: 1.5rem !important;
          font: inherit;
          resize: both;
          white-space: pre;
          overflow: hidden;
          text-overflow: ellipsis;
          overflow-wrap: normal;
          width: 100%;
        }
        /* only needed for contenteditable elements */
        .code:empty::after {
          color: gray;
          content: attr(placeholder);
        }
        .code.invalid {
          border: 1px solid red;
        }
        #code-wrapper.invalid {
          border: 2px solid red;
        }
        mark {
          background-color: transparent;
          color: red;
          font-weight: bolder;
          text-decoration: red wavy underline;
          text-underline-position: under;
          /* text-underline-offset: 1px; */
          text-decoration-skip-ink: none;
        }
      `,
    ];
  }

  /* Note
   We cannot set the content of a contenteditable div like this: <div>${content}</div>
   as it interferes with lit-html. We need to do use innerHTML: <div .innerHTML=${content}></div>
  */

  getFilterPart(item) {
    if (item.name.startsWith('template')) {
      return html`${item.lines?.join('\n')}`;
    }
    const indent = ' '.repeat(item.indent);
    const dynamicHTML = `${item.lines?.join('\n')}`;
    return html`\n${indent}<div
      id="${item.name}"
      class="code"
      style="max-width: calc(100% - ${item.indent}ch);"
      contenteditable="true"
      spellcheck="false"
      @blur=${this._change}
      .innerHTML=${dynamicHTML}
      placeholder="Your code goes here"></div>\n`;
  }

  render() {
    const codeToolTip = getToolTip(this.model.diagnostics);
    const result = html`
      <div id="code-wrapper"
        class="border p-2 ${this.model.diagnostics.length ? 'invalid' : ''}"
        title=${codeToolTip}><pre>${repeat(
          this.model.parts,
          item => item.name,
          item => this.getFilterPart(item)
        )}</pre></div>
    `;
    return result;
  }

}

window.customElements.define('filter-edit', FilterEdit);
