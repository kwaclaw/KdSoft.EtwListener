/* global i18n */

import { repeat } from 'lit-html/directives/repeat.js';
import { LitMvvmElement, css, html, nothing, BatchScheduler } from '@kdsoft/lit-mvvm';
import '@kdsoft/lit-mvvm-components/kds-expander.js';
import checkboxStyles from '../styles/kds-checkbox-styles.js';
import fontAwesomeStyles from '../styles/fontawesome/css/all-styles.js';
import tailwindStyles from '../styles/tailwind-styles.js';
import spinnerStyles from '../styles/spinner-styles.js';
import appStyles from '../styles/etw-app-styles.js';
import gridStyles from '../styles/kds-grid-styles.js';
import '../components/etw-checklist.js';

class EtwErrorList extends LitMvvmElement {
  _errorDetailClick(e) {
    e.currentTarget.classList.toggle('show-detail');
  }

  _closeError() {
    this.model.showErrors = false;
    this.model.showLastError = false;
  }

  _errorGridDown(e) {
    this.model.keepErrorsOpen();
  }

  //#region overrides

  /* eslint-disable indent, no-else-return */

  disconnectedCallback() {
    super.disconnectedCallback();
  }

  shouldRender() {
    return !!this.model;
  }

  beforeFirstRender() {
    //
  }

  // called at most once every time after connectedCallback was executed
  firstRendered() {
    //
  }

  static get styles() {
    return [
      tailwindStyles,
      checkboxStyles,
      fontAwesomeStyles,
      appStyles,
      gridStyles,
      css`
        :host {
          display: block;
          position: relative;
        }

        #error-resizable {
          position: relative;
          height: var(--error-height, 0px);
        }

        #error-grid {
          box-sizing: border-box;
          position: absolute;
          left: 0;
          right: 0;
          top: 0;
          bottom: 0;
          grid-template-columns: fit-content(18em) 1fr;
        }

        #error-close {
          position: sticky;
          top: 0;
          justify-self: end;
          grid-column: 1/-1;
        }

        #error-grid .kds-row > div {
          background-color: #feb2b2;
        }

        #error-grid .kds-row > pre {
          grid-column: 1/-1;
          margin-left: 3em;
          padding: 8px 4px;
          outline: 1px solid #c8c8c8;
          overflow: hidden;
          white-space: nowrap;
          text-overflow: ellipsis;
        }

        #error-grid .kds-row > .show-detail {
          max-height: 300px;
          overflow: scroll;
          white-space: pre;
        }
      `
    ];
  }

  render() {
    return html`
      <style>
        :host {
          --error-height: ${this.model.errorHeight};
        }
      </style>
      <div id="error-resizable">
        <div id="error-grid" class="kds-container px-2 pt-0 pb-2" @pointerdown=${this._errorGridDown}>
        <button id="error-close" class="p-1 text-gray-500" @click=${this._closeError}>
          <span aria-hidden="true" class="fas fa-lg fa-times"></span>
        </button>
        ${repeat(
          this.model.fetchErrors.reverseItemIterator(),
          item => item.sequenceNo,
          item => {
            if (item instanceof Error) {
              return html`
                <div class="kds-row">
                  <div>${item.timeStamp}</div>
                  <div>${item.name}: ${item.message}</div>
                  ${item.fileName ? html`<div>${item.fileName} (${item.lineNumber}:${item.columnNumber})</div>` : ''}
                  ${item.stack ? html`<pre @click=${this._errorDetailClick}>${item.stack}</pre>` : ''}
                </div>
              `;
            } else {
              return html`
                <div class="kds-row">
                  <div>${item.timeStamp}</div>
                  <div>${item.title}</div>
                  <pre @click=${this._errorDetailClick}>${item.detail}</pre>
                </div>
              `;
            }
          }
        )}
        </div>
    `;
  }

  //#endregion
}

window.customElements.define('etw-error-list', EtwErrorList);
