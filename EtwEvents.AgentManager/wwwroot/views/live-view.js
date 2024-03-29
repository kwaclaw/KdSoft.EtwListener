﻿import { repeat } from 'lit-html/directives/repeat.js';
import { observe, unobserve } from '@nx-js/observer-util';
import { LitMvvmElement, css, html, nothing, BatchScheduler } from '@kdsoft/lit-mvvm';
import tailwindStyles from '../styles/tailwind-styles.js';
import checkboxStyles from '../styles/kds-checkbox-styles.js';
import fontAwesomeStyles from '../styles/fontawesome/css/all-styles.js';
import gridStyles from '../styles/kds-grid-styles.js';
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

  disconnectedCallback() {
    if (this._columnObserver) {
      unobserve(this._columnObserver);
      this._columnObserver = null;
    }
    super.disconnectedCallback();
  }

  shouldRender() {
    return !!this.model;
  }

  beforeFirstRender() {
    const lvcm = this.model.liveViewConfigModel;

    this._columnObserver = observe(() => {
      const standardCols = lvcm.getSelectedStandardColumns();
      const payloadCols = lvcm.getSelectedPayloadColumns();
      // don't trigger a render unnecessarily
      if (utils.targetEquals(this._standardCols, standardCols) && utils.targetEquals(this._payloadCols, payloadCols)) {
        return;
      }

      this._standardCols = standardCols;
      this._payloadCols = payloadCols;
      this._expandPayload = standardCols.findIndex(col => col.name === 'payload') < 0;
      this._colTemplate = Array(standardCols.length + payloadCols.length).fill('auto').join(' ');
      // trigger render as we don't observe columns in the render() method for performance reasons
      this.model.__changeCount += 1;
    });
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
