import { html, nothing } from 'lit';
import { repeat } from 'lit/directives/repeat.js';
import { LitMvvmElement, css, BatchScheduler } from '@kdsoft/lit-mvvm';
import tailwindStyles from '@kdsoft/lit-mvvm-components/styles/tailwind-styles.js';
import checkboxStyles from '@kdsoft/lit-mvvm-components/styles/kdsoft-checkbox-styles.js';
import fontAwesomeStyles from '@kdsoft/lit-mvvm-components/styles/fontawesome/css/all-styles.js';
import gridStyles from '../styles/kdsoft-grid-styles.js';
import appStyles from '../styles/etw-app-styles.js';
import * as utils from '../js/utils.js';

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

class LiveView extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new BatchScheduler(100);
  }

  _gridClick(e) {
    //e.preventDefault();
    const row = e.target.closest('.kds-row');
    if (!row) return;
    const payload = row.querySelector('.payload');
    if (!payload) return;
    payload.toggleAttribute('hidden');
  }

  /* eslint-disable indent, no-else-return */

  shouldRender() {
    return !!this.model;
  }

  beforeFirstRender() {
    const lvm = this.model;

    const sclist = lvm.liveViewConfigModel.getStandardColumnList();
    this._standardCols = lvm.liveViewConfigModel.standardColumns.map(col => sclist[col]);
    this._expandPayload = this._standardCols.findIndex(col => col.name === 'payload') < 0;

    const pclist = lvm.liveViewConfigModel.payloadColumnList;
    this._payloadCols = lvm.liveViewConfigModel.payloadColumns.map(pcol => pclist[pcol]);

    this._colTemplate = Array(this._standardCols.length + this._payloadCols.length).fill('auto').join(' ');
  }

  rendered() {
    const grid = this.renderRoot.getElementById('grid');
    grid.scrollTop = grid.scrollHeight;
  }

  static get styles() {
    return [
      tailwindStyles,
      checkboxStyles,
      fontAwesomeStyles,
      gridStyles,
      appStyles,
      css`
        :host {
          display: block;
          position: relative;
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

        .kds-header.payload {
          font-style: italic;
        }

        .kds-row > .payload {
          grid-column: 1/-1;
        }
      `,
    ];
  }

  render() {
    const lvm = this.model;

    const itemIterator = (lvm && lvm.liveEvents) ? lvm.liveEvents.itemIterator() : utils.emptyIterator();

    const result = html`
      <style>
        #grid {
          grid-template-columns: ${this._colTemplate};
        }
      </style>
      <div id="container" class="border">
        <div id="grid" class="kds-container" @click=${this._gridClick}>
          <div class="kds-header-row">
            ${this._standardCols.map(col => html`<div class="kds-header">${col.label}</div>`)}
            ${this._payloadCols.map(col => html`<div class="kds-header payload">${col.label}</div>`)}
          </div>
          ${repeat(
            itemIterator,
            item => item._seqNo,
            item => html`
                <div class="kds-row">
                  ${this._standardCols.map(col => html`<div>${renderColumn(item[col.name], col.type)}</div>`)}
                  ${this._payloadCols.map(col => html`<div>${renderColumn(item.payload[col.name], col.type)}</div>`)}
                  ${this._expandPayload
                    ? html`<div class="payload" hidden><pre>${JSON.stringify(item.payload, null, 2)}</pre></div>`
                    : nothing
                  }
                </div>
              `
          )}
        </div>
      </div>
    `;
    return result;
  }
}

window.customElements.define('live-view', LiveView);
