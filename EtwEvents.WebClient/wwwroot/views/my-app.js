/* global i18n */

import { html, nothing } from '../lib/lit-html.js';
import { repeat } from '../lib/lit-html/directives/repeat.js';
import { classMap } from '../lib/lit-html/directives/class-map.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { LitMvvmElement, BatchScheduler } from '../lib/@kdsoft/lit-mvvm.js';
import { css } from '../styles/css-tag.js';
import './my-app-side-bar.js';
import './trace-session-view.js';
import './filter-form.js';
import Spinner from '../js/spinner.js';
import sharedStyles from '../styles/kdsoft-shared-styles.js';
import { KdSoftGridStyle } from '../styles/kdsoft-grid-style.js';
import myappStyleLinks from '../styles/my-app-style-links.js';

const runBtnBase = { fas: true };
const tabBase = { 'inline-block': true, 'py-2': true, 'no-underline': true };

const classList = {
  startBtn: { ...runBtnBase, 'fa-play': true, 'text-green-500': true },
  stopBtn: { ...runBtnBase, 'fa-stop': true, 'text-red-500': true },
  tabActive: { ...tabBase, 'pl-4': true, 'pr-2': true, 'text-white': true },
  tabInactive: { ...tabBase, 'px-4': true, 'text-gray-600': true, 'hover:text-gray-200': true, 'hover:text-underline': true },
  tabButtonsActive: { 'inline-block': true },
  tabButtonsInActive: { hidden: true }
};


class MyApp extends LitMvvmElement {
  constructor() {
    super();
    // setting model property here because we cannot reliable set it from a non-lit-html rendered HTML page
    this.scheduler = new Queue(priorities.HIGH);
    // we must assign the model *after* the scheduler, or assign it externally
    // this.model = new MyAppModel(); --

    window.myapp = this;
  }

  //#region trace-session

  _sessionTabClick(e) {
    const linkElement = e.currentTarget.closest('li');
    this.model.activateSession(linkElement.dataset.sessionName);
  }

  _unwatchSessionClick(e, session) {
    e.preventDefault();
    e.stopPropagation();
    if (!session) return;
    this.model.unwatchSession(session);
  }

  _filterSessionClick(e, session) {
    if (!session) return;
    const sidebar = this.renderRoot.getElementById('sidebar');
    sidebar.showFilterDlg(session);
  }

  _toggleSessionEvents(e, session) {
    if (!session) return;
    const spinner = new Spinner(e.currentTarget);
    session.toggleEvents(spinner);
  }

  //#endregion

  //#region error handling

  _showErrors() {
    this.model.showErrors = true;
  }

  _errSizeDown(e) {
    if (e.buttons !== 1) return;

    this._resizeEl = this.renderRoot.getElementById('error-resizable');
    this.model.keepErrorsOpen();

    e.currentTarget.setPointerCapture(e.pointerId);
    e.currentTarget.onpointermove = ev => this._errSizeChange(ev);
  }

  _errSizeChange(e) {
    if (!this._resizeEl) return;

    const h = this._resizeEl.offsetHeight;
    const dy = e.offsetY;
    if (e.y === 0 || dy === 0) return;

    const newHeightStyle = `${h - dy}px`;
    this._resizeEl.style.height = newHeightStyle;
  }

  _errSizeUp(e) {
    this._resizeEl = null;
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

  connectedCallback() {
    super.connectedCallback();
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this.model.unobserveVisibleSessions();
  }

  // called at most once every time after connectedCallback was executed
  firstRendered() {
    super.firstRendered();
    this.appTitle = this.getAttribute('appTitle');
    this.model.observeVisibleSessions();
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
      KdSoftGridStyle,
      css`
        :host {
          display: block;
        }
        
        #container {
          position: relative;
          height: 100%;
          display: grid;
          grid-template-columns: minmax(2rem, 1fr) 3fr;
          grid-template-rows: 1fr auto;
        }

        #sidebar {
          grid-column: 1;
          grid-row: 1/2;
        }

        #main {
          grid-column: 2;
          grid-row: 1/2;
          height: 100%;
          position: relative;
          display: flex;
          flex-direction: column;
          flex-wrap: nowrap;
          justify-content: flex-start;
          align-items: stretch;
        }

        footer {
          grid-column: 1/-1;
          grid-row: 3;
        }

        .brand {
          font-family: Candara;
        }

        #tab-buttons button {
          padding-left: 0.25rem;
          padding-right: 0.25rem;
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
          grid-template-columns: fit-content(14em) 1fr;
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

  /* eslint-disable indent, no-else-return */

  shouldRender() {
    return !!this.model;
  }

  render() {
    return html`
      ${sharedStyles}
      <link rel="stylesheet" type="text/css" href=${myappStyleLinks.myapp} />
      <link rel="stylesheet" type="text/css" href="css/spinner.css" />
      <style>
        :host {
          position: relative;
        }
      </style>

      <div id="container">

        <my-app-side-bar id="sidebar" .model=${this.model}></my-app-side-bar>

        <div id="main">
          <div id="nav-content" class="lg:flex lg:items-center lg:w-auto hidden lg:block pt-6 lg:pt-0 bg-gray-500">
            <ul class="list-reset lg:flex justify-end flex-1 items-center">
            ${this.model.visibleSessions.map(ses => {
              const isActiveTab = this.model.activeSession === ses;
              const tabClasses = isActiveTab ? classList.tabActive : classList.tabInactive;
              const eventsClasses = ses.state.isRunning ? classList.stopBtn : classList.startBtn;

              return html`
                <li class="mr-2 pr-1 ${isActiveTab ? 'bg-gray-700' : ''}" data-session-name=${ses.name.toLowerCase()} @click=${this._sessionTabClick}>
                  <a class=${classMap(tabClasses)} href="#">${ses.name}</a>
                  <div id="tab-buttons" class=${classMap(isActiveTab ? classList.tabButtonsActive : classList.tabButtonsInActive)}>
                    <button type="button" @click=${e => this._toggleSessionEvents(e, ses)}>
                      <i class=${classMap(eventsClasses)}></i>
                    </button>
                    <button type="button" class="text-gray-500" @click=${e => this._filterSessionClick(e, ses)}>
                      <i class="fas fa-filter"></i>
                    </button>
                    <button type="button" class="text-gray-500" @click=${e => this._unwatchSessionClick(e, ses)}>
                      <i class="fas fa-lg fa-times"></i>
                    </button>
                  </div>
                </li>
                `;
              }
            )}
            </ul>
          </div>

          <!-- Main content -->
          <div class="flex-grow relative">
            <trace-session-view .model=${this.model.activeSession}></trace-session-view>
          </div>
        </div>

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
                  item => html`
                      <div class="kds-row">
                      <div>${item.timeStamp}</div>
                      <div>${item.title}</div>
                      <pre @click=${this._errorDetailClick}>${item.detail}</pre>
                      </div>
                    `)
                  }
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

window.customElements.define('my-app', MyApp);
