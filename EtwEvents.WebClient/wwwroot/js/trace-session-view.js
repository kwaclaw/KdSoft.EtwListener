import { html } from '../lib/lit-html.js';
import { LitMvvmElement, BatchScheduler } from '../lib/@kdsoft/lit-mvvm.js';
import { repeat } from '../lib/lit-html/directives/repeat.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { css } from '../styles/css-tag.js';
import styleLinks from '../styles/kdsoft-style-links.js';
import { SyncFusionGridStyle } from '../styles/css-grid-syncfusion-style.js';
import * as utils from './utils.js';

class TraceSessionView extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new BatchScheduler(100);

    //this._dtFormat = new Intl.DateTimeFormat('default', { dateStyle: 'short', timeStyle: 'short' });
    this._dtFormat = new Intl.DateTimeFormat('default', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: 'numeric',
      minute: 'numeric',
      second: 'numeric',
      milli: 'numeric'
    });
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
      SyncFusionGridStyle,
      css`
        #container {
          height: 100%
        }

        #grid {
          grid-template-columns: auto auto auto auto auto auto;
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
      `,
    ];
  }

  render() {
    const ts = this.model;
    const itemIterator = (ts && ts.eventSession) ? ts.eventSession.itemIterator() : utils.emptyIterator();

    const result = html`
      <link rel="stylesheet" type="text/css" href=${styleLinks.tailwind} />
      <link rel="stylesheet" type="text/css" href=${styleLinks.fontawesome} />
      <style>
        :host {
          display: block;
        }
      </style>
      <div id="container" class="border">
        <div id="grid" class="sfg-container">
          <div class="sfg-header-row">
            <div class="sfg-header">Sequence No</div>
            <div class="sfg-header">Task</div>
            <div class="sfg-header">OpCode</div>
            <div class="sfg-header">TimeStamp</div>
            <div class="sfg-header">Level</div>
            <div class="sfg-header">Payload</div>
          </div>
          ${repeat(
            itemIterator,
            item => item.sequenceNo,
            (item, indx) => {
              //const dateString = this._dtFormat.format(new Date(item.timeStamp));
              const dateString = `${this._dtFormat.format(item.timeStamp)}.${item.timeStamp % 1000}`;
              return html`
            <div class="sfg-row">
              <div>${item.sequenceNo}</div>
              <div>${item.taskName}</div>
              <div>${item.opCode}</div>
              <div>${dateString}</div>
              <div>${item.level}</div>
              <div><pre>${JSON.stringify(item.payload)}</pre></div>
            </div>`;
            }
          )}
        </div>
      </div>
    `;
    return result;
  }
}

window.customElements.define('trace-session-view', TraceSessionView);
