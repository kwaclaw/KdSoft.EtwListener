﻿import { html } from '../lib/lit-html.js';
import { LitMvvmElement, BatchScheduler } from '../lib/@kdsoft/lit-mvvm.js';
import { repeat } from '../lib/lit-html/directives/repeat.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { css } from '../styles/css-tag.js';
import styleLinks from '../styles/kdsoft-style-links.js';
import { KdSoftGridStyle } from '../styles/kdsoft-grid-style.js';
import * as utils from './utils.js';

function renderColumn(colValue, colType) {
  // variable != null checks for both, != undefined and != null, without being falsy for 0
  if (colValue != null) {
    switch (colType) {
      case 'number':
        return colValue.toString();
      case 'string':
        return colValue;
      case 'date':
        return `${utils.dateFormat.format(colValue)}.${colValue % 1000}`;
      case 'object':
        return JSON.stringify(colValue);
      default:
        return colValue;
    }
  }
  return '';
}

class TraceSessionView extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new BatchScheduler(100);
  }

  connectedCallback() {
    super.connectedCallback();
  }

  firstRendered() {
    //
  }

  rendered() {
    const grid = this.renderRoot.getElementById('grid');
    grid.scrollTop = grid.scrollHeight;
  }

  static get styles() {
    return [
      KdSoftGridStyle,
      css`
        #container {
          height: 100%
        }

        #grid {
          position: absolute;
          top: 0;
          bottom: 0;
          left: 0;
          right: 0;
          overflow-x: auto;
          overflow-y: auto;
          -webkit-overflow-scrolling: touch;
          pointer-events: auto;
          z-index: 20;
        }

        .payload {
          font-style: italic;
        }
      `,
    ];
  }

  /* eslint-disable indent, no-else-return */

  render() {
    const ts = this.model;

    //TODO create the column lists only on/before first render, as in: if (!this.firstRendered) { ... }

    if (!this._firstRendered) {
      const sclist = ts.profile.getStandardColumnList();
      this._standardCols = ts.profile.standardColumns.map(col => sclist[col]);

      const pclist = ts.profile.payloadColumnList;
      this._payloadCols = ts.profile.payloadColumns.map(pcol => pclist[pcol]);

      this._colTemplate = Array(this._standardCols.length + this._payloadCols.length).fill('auto').join(' ');
    }

    const itemIterator = (ts && ts.eventSession) ? ts.eventSession.itemIterator() : utils.emptyIterator();

    const result = html`
      <link rel="stylesheet" type="text/css" href=${styleLinks.tailwind} />
      <link rel="stylesheet" type="text/css" href=${styleLinks.fontawesome} />
      <style>
        :host {
          display: block;
        }
        #grid {
          grid-template-columns: ${this._colTemplate};
        }
      </style>
      <div id="container" class="border">
        <div id="grid" class="kds-container">
          <div class="kds-header-row">
            ${this._standardCols.map(col => html`<div class="kds-header">${col.label}</div>`)}
            ${this._payloadCols.map(col => html`<div class="kds-header payload">${col.label}</div>`)}
          </div>
          ${repeat(
            itemIterator,
            item => item.sequenceNo,
            (item, indx) => {
              return html`
                <div class="kds-row">
                  ${this._standardCols.map(col => html`<div>${renderColumn(item[col.name], col.type)}</div>`)}
                  ${this._payloadCols.map(col => html`<div>${renderColumn(item.payload[col.name], col.type)}</div>`)}
                </div>
              `;
            }
          )}
        </div>
      </div>
    `;
    return result;
  }
}

window.customElements.define('trace-session-view', TraceSessionView);
