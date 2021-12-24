/* global i18n */

import { html, nothing } from 'lit';
import { repeat } from 'lit/directives/repeat.js';
import { Queue, priorities } from '@nx-js/queue-util/dist/es.es6.js';
import { LitMvvmElement, css } from '@kdsoft/lit-mvvm';
import './etw-app-side-bar.js';
import './etw-agent.js';
import checkboxStyles from '@kdsoft/lit-mvvm-components/styles/kdsoft-checkbox-styles.js';
import fontAwesomeStyles from '@kdsoft/lit-mvvm-components/styles/fontawesome/css/all-styles.js';
import tailwindStyles from '../styles/tailwind-styles.js';
import gridStyles from '../styles/kdsoft-grid-styles.js';

class EtwApp extends LitMvvmElement {
  constructor() {
    super();

    // setting model property here because we cannot reliable set it from a non-lit-html rendered HTML page
    this.scheduler = new Queue(priorities.HIGH);
    // we must assign the model *after* the scheduler, or assign it externally
    // this.model = new EtwAppModel(); --

    this.sideBarWidth = '24rem';
    window.etwApp = this;
  }

  _sidebarObserverCallback(mutations) {
    mutations.forEach(mutation => {
      switch (mutation.type) {
        case 'attributes':
          /* An attribute value changed on the element in mutation.target; the attribute name is in
             mutation.attributeName and its previous value is in mutation.oldValue */
          if (mutation.attributeName === 'aria-expanded') {
            this._sidebarExpandedChanged(mutation.oldValue);
          }
          break;
        default:
          break;
      }
    });
  }

  //#region sidebar

  _sidebarExpandedChanged(oldValue) {
    const cntr = this.renderRoot.getElementById('container');
    // remove resize style, as it would interfere with collapsing/expanding
    cntr.style.gridTemplateColumns = '';
    const wasExpanded = !!oldValue;
    if (wasExpanded) cntr.classList.remove('sidebar-expanded');
    else cntr.classList.add('sidebar-expanded');
  }

  _sidebarSizeDown(e) {
    if (e.buttons !== 1) return;

    this._gridContainerEl = this.renderRoot.querySelector('#container.sidebar-expanded');
    if (!this._gridContainerEl) return;

    //disable expand/collapse transition for resizing
    this._gridContainerEl.style.transition = 'none';

    e.currentTarget.setPointerCapture(e.pointerId);
    e.currentTarget.onpointermove = ev => this._sidebarSizeChange(ev);
  }

  _sidebarSizeChange(e) {
    if (this._gridContainerEl) {
      this.sideBarWidth = `${e.x}px`;
      //trigger a re-render so that the CSS variable gets updated
      this.model.__changeCount += 1;
    }
  }

  _sidebarSizeUp(e) {
    //re-ensable expand/collapse transition
    this._gridContainerEl.style.transition = '';

    this._gridContainerEl = null;
    e.currentTarget.releasePointerCapture(e.pointerId);
    e.currentTarget.onpointermove = null;
  }

  //#endregion

  //#region error handling

  _showErrors() {
    this.model.showErrors = true;
  }

  _errSizeDown(e) {
    if (e.buttons !== 1) return;

    this._errorResizeEl = this.renderRoot.getElementById('error-resizable');
    this.model.keepErrorsOpen();

    e.currentTarget.setPointerCapture(e.pointerId);
    e.currentTarget.onpointermove = ev => this._errSizeChange(ev);
  }

  _errSizeChange(e) {
    if (!this._errorResizeEl) return;

    const h = this._errorResizeEl.offsetHeight;
    const dy = e.offsetY;
    if (e.y === 0 || dy === 0) return;

    const newHeightStyle = `${h - dy}px`;
    this._errorResizeEl.style.height = newHeightStyle;
  }

  _errSizeUp(e) {
    this._errorResizeEl = null;
    e.currentTarget.releasePointerCapture(e.pointerId);
    e.currentTarget.onpointermove = null;
  }

  _errorGridDown(e) {
    this.model.keepErrorsOpen();
  }

  _errorDetailClick(e) {
    e.currentTarget.classList.toggle('show-detail');
  }

  _closeError() {
    this.model.showErrors = false;
    this.model.showLastError = false;
  }

  // assumes error is problem details object - see https://tools.ietf.org/html/rfc7807
  defaultHandleError(error) {
    this.model.handleFetchError(error);
  }

  //#endregion

  //#region overrides

  /* eslint-disable indent, no-else-return */

  disconnectedCallback() {
    super.disconnectedCallback();
    this._sidebarObserver.disconnect();
  }

  shouldRender() {
    return !!this.model;
  }

  // called at most once every time after connectedCallback was executed
  firstRendered() {
    this.appTitle = this.getAttribute('appTitle');

    // listen for changes of the aria-expanded attribute on sidebar,
    // and expand/collapse the first grid column accordingly
    this._sidebarObserver = new MutationObserver(m => this._sidebarObserverCallback(m));
    const sidebar = this.renderRoot.getElementById('sidebar');
    this._sidebarObserver.observe(sidebar, { attributes: true, attributeOldValue: true, attributeFilter: ['aria-expanded'] });
  }

  rendered() {
    let newHeightStyle = null;
    if (this.model.showErrors) {
      newHeightStyle = `${300}px`;
    } else if (this.model.showLastError) {
      // Calculate the height of the first row in errors, and resize the errors
      // container to show just that row, as it contains the most recent error.
      const topRow = this.renderRoot.querySelector('#error-grid div.kds-row');
      if (topRow) {
        let minTop = 0;
        let maxBottom = 0;
        // topRow has CSS rule "display: contents", so it has no height of its own
        for (let indx = 0; indx < topRow.children.length; indx += 1) {
          const child = topRow.children[indx];
          const ot = child.offsetTop;
          const ob = ot + child.offsetHeight;
          if (ot < minTop) minTop = ot;
          if (ob > maxBottom) maxBottom = ob;
        }
        const topRowHeight = maxBottom - minTop;
        newHeightStyle = `${topRowHeight}px`;
      }
    }

    if (newHeightStyle) {
      const errContainer = this.renderRoot.getElementById('error-resizable');
      errContainer.style.height = newHeightStyle;
    }
  }

  static get styles() {
    return [
      tailwindStyles,
      checkboxStyles,
      fontAwesomeStyles,
      gridStyles,
      css`
        :host {
          display: block;
          position: relative;
        }
        
        #container {
          position: relative;
          height: 100%;
          display: grid;
          grid-template-columns: 0px 0px 1fr;
          grid-template-rows: 1fr auto;
          transition: grid-template-columns var(--trans-time, 300ms) ease;
        }

        #container.sidebar-expanded {
          grid-template-columns: var(--side-bar-width) 4px 1fr;
        }

        #sidebar {
          grid-column: 1;
          grid-row: 1/2;
          overflow-x: hidden;
        }

        #sidebar-resize {
          grid-column: 2;
          grid-row: 1/2;
          /* width: 3px; */
          border-left: solid gray 1px;
          border-right: solid gray 1px;
          height: 100%;
          cursor: e-resize;
        }

        #main {
          grid-column: 3;
          grid-row: 1/2;
        }

        footer {
          grid-column: 1/-1;
          grid-row: 3;
        }

        #error-resize {
          height: 3px;
          border-top: solid gray 1px;
          border-bottom: solid gray 1px;
          width: 100%;
          cursor: n-resize;
        }

        #error-resizable {
          position: relative;
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
          --side-bar-width: ${this.sideBarWidth};
        }
      </style>
      <div id="container" class="sidebar-expanded">

        <etw-app-side-bar id="sidebar" .model=${this.model} aria-expanded="true"></etw-app-side-bar>

        <div id="sidebar-resize" @pointerdown=${this._sidebarSizeDown} @pointerup=${this._sidebarSizeUp}></div>

        <etw-agent id="main" .model=${this.model}></etw-agent>

        <footer>
          ${(!this.model.showLastError && !this.model.showErrors)
            ? nothing
            : html`
              <div id="error-resize" @pointerdown=${this._errSizeDown} @pointerup=${this._errSizeUp}></div>

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
              </div>
            `
          }
          <div class="flex p-2 border bg-gray-800 text-white">&copy; Karl Waclawek
            <button class="ml-auto" @click=${this._showErrors}>
              ${this.model.fetchErrors.count()} ${i18n.__('Errors')}
            </button>
          </div>
        </footer>

      </div>
    `;
  }

  //#endregion
}

window.customElements.define('etw-app', EtwApp);
