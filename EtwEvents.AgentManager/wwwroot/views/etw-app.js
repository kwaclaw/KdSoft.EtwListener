/* global i18n */

import { html, nothing } from 'lit';
import { classMap } from 'lit/directives/class-map.js';
import { repeat } from 'lit/directives/repeat.js';
import { Queue, priorities } from '@nx-js/queue-util';
import { LitMvvmElement, css, BatchScheduler } from '@kdsoft/lit-mvvm';
import './etw-app-side-bar.js';
import './etw-agent.js';
import './live-view.js';
import * as utils from '../js/utils.js';
import checkboxStyles from '@kdsoft/lit-mvvm-components/styles/kdsoft-checkbox-styles.js';
import fontAwesomeStyles from '@kdsoft/lit-mvvm-components/styles/fontawesome/css/all-styles.js';
import tailwindStyles from '../styles/tailwind-styles.js';
import gridStyles from '../styles/kdsoft-grid-styles.js';

const tabBase = {
  'pt-3': true,
  'pb-2': true,
  'px-6': true,
  block: true,
  'focus:outline-none': true
};

const certInfoBase = {
  'px-6': true,
  block: true,
  'focus:outline-none': true,
  'ml-auto': true
};

const tabClassList = {
  tabActive: {
    ...tabBase,
    'text-blue-400': true,
    'hover:text-blue-400': true,
    'bg-gray-700': true,
    'border-b-2': true,
    'font-medium': true,
    'border-blue-500': true
  },
  tabInactive: {
    ...tabBase,
    'text-gray-300': true,
    'hover:text-blue-400': true
  },
  certInfo: {
    ...certInfoBase,
    'text-gray-300': true
  },
  certInfoWarning: {
    ...certInfoBase,
    'text-red-500': true
  }
};

class EtwApp extends LitMvvmElement {
  constructor() {
    super();
    //this.scheduler = new Queue(priorities.LOW);
    //this.scheduler = new BatchScheduler(0);
    this.scheduler = window.renderScheduler = cb => window.queueMicrotask(cb);

    // setting model property here because we cannot reliable set it from a non-lit-html rendered HTML page
    // we must assign the model *after* the scheduler, or assign it externally
    // this.model = new EtwAppModel(); --

    window.etwApp = this;
  }

  _sidebarObserverCallback(mutations) {
    mutations.forEach(mutation => {
      switch (mutation.type) {
        case 'attributes':
          /* An attribute value changed on the element in mutation.target; the attribute name is in
             mutation.attributeName and its previous value is in mutation.oldValue */
          if (mutation.attributeName === 'aria-expanded') {
            this._sidebarCollapsedChanged(mutation.oldValue);
          }
          break;
        default:
          break;
      }
    });
  }

  //#region sidebar

  _sidebarCollapsedChanged(oldValue) {
    const cntr = this.renderRoot.getElementById('container');
    // remove resize style, as it would interfere with collapsing/expanding
    cntr.style.gridTemplateColumns = '';
    const wasCollapsed = !!oldValue;
    if (wasCollapsed) cntr.classList.remove('sidebar-collapsed');
    else cntr.classList.add('sidebar-collapsed');
  }

  _sidebarSizeDown(e) {
    if (e.buttons !== 1) return;

    this._gridContainerEl = this.renderRoot.querySelector('#container');
    if (this._gridContainerEl.classList.contains('sidebar-collapsed')) return;

    //disable expand/collapse transition for resizing
    this._gridContainerEl.style.transition = 'none';

    e.currentTarget.setPointerCapture(e.pointerId);
    e.currentTarget.onpointermove = ev => this._sidebarSizeChange(ev);
  }

  _sidebarSizeChange(e) {
    if (this._gridContainerEl) {
      this.model.sideBarWidth = `${e.x}px`;
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

    let height = h - dy;
    if (height < 0) height = 0;

    // we don't want to trigger scroll bars on the container
    const el = e.currentTarget.parentElement.parentElement;
    const overHeight = el.scrollHeight - el.offsetHeight;
    if (overHeight > 0) {
      height = this.model.lastErrorHeight - overHeight;
    }
    this.model.lastErrorHeight = height;

    this.model.errorHeight = `${height}px`;
    //  const newHeightStyle = `${h - dy}px`;
    //  this._errorResizeEl.style.height = newHeightStyle;
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

  //#region agent tabs

  _agentTabClick(e) {
    e.stopPropagation();
    const div = e.target.closest('div');
    if (!div) return;
    this.model.activeAgentTab = div.dataset.tabid;
  }

  _tabClassType(tabId) {
    return tabClassList[this.model.activeAgentTab === tabId ? 'tabActive' : 'tabInactive'];
  }

  _tabSectionClass(tabId) {
    return this.model.activeAgentTab === tabId ? 'active' : '';
  }

  _toggleEtwEvents(activeEntry) {
    const currentState = activeEntry?.current;
    if (currentState && currentState.isRunning) {
      if (this.model.etwEventSource) this.model.stopEtwEvents();
      else this.model.getEtwEvents(activeEntry?.state);
    }
  }

  _exportAgentConfig(agentState) {
    if (!agentState) {
      return;
    }
    const exportObject = this.model.getAgentOptions(agentState);
    const exportString = JSON.stringify(exportObject, null, 2);
    const exportURL = `data:text/plain,${exportString}`;

    const a = document.createElement('a');
    try {
      a.style.display = 'none';
      a.href = exportURL;
      a.download = `${agentState.id}.json`;
      document.body.appendChild(a);
      a.click();
    } finally {
      document.body.removeChild(a);
    }
  }

  _importAgentConfigDialog(e) {
    const fileDlg = e.currentTarget.parentElement.querySelector('input[type="file"]');
    // reset value so that @change event fires reliably
    fileDlg.value = null;
    fileDlg.click();
  }

  _importAgentConfig(e, entry) {
    const selectedFile = e.currentTarget.files[0];
    if (!selectedFile) return;

    selectedFile.text().then(txt => {
      const importObject = JSON.parse(txt);
      this.model.setAgentOptions(entry.state, importObject);
    });
  }

  //#endregion

  //#region overrides

  /* eslint-disable indent, no-else-return */

  disconnectedCallback() {
    this._sidebarObserver?.disconnect();
    super.disconnectedCallback();
  }

  shouldRender() {
    return !!this.model;
  }

  beforeFirstRender() {
    this.model.sideBarWidth = '24rem';
    this.model.errorHeight = '2rem';
    this.model.activeAgentTab = 'agent-config';
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
      newHeightStyle = this.model.errorHeight;
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
      this.model.errorHeight = newHeightStyle;
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
          grid-template-columns: var(--side-bar-width) 4px 1fr;
          grid-template-rows: 1fr auto;
          transition: grid-template-columns var(--trans-time, 300ms) ease;
        }

        #container.sidebar-collapsed {
          grid-template-columns: 2rem 0px 1fr;
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
          position: relative;
          display: flex;
          flex-direction: column;
        }

        #agent {
          position: relative;
          height: 100%;
        }

        #agent > .section:not(.active) {
          display: none !important;
        }

        etw-agent {
          position: absolute;
          top: 0;
          bottom: 0;
          left: 0;
          right: 0;
          overflow-y: auto;
        }

        live-view {
          position: absolute;
          top: 0;
          bottom: 0;
          left: 0;
          right: 0;
          overflow-y: auto;
        }

        footer {
          grid-column: 1/-1;
          grid-row: 3;
        }

        #error-resize {
          height: 4px;
          border-top: solid gray 1px;
          border-bottom: solid gray 1px;
          width: 100%;
          cursor: n-resize;
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
    const activeEntry = this.model.getActiveEntry();
    const activeAgentState = this.model.activeAgentState;

    const agentConfigTabType = this._tabClassType('agent-config');
    const agentLiveViewTabType = this._tabClassType('agent-live-view');
    const certInfoClassType = (window.clientCertLifeDays < window.certExpiryWarningDays) ? tabClassList.certInfoWarning : tabClassList.certInfo;

    const isRunning = activeEntry?.current?.isRunning;
    const isLive = !!this.model.etwEventSource;
    const sinkClass = isRunning ? (isLive ? 'text-yellow-300' : 'text-yellow-600') : 'text-gray-300';

    return html`
      <style>
        :host {
          --side-bar-width: ${this.model.sideBarWidth};
          --error-height: ${this.model.errorHeight};
        }
      </style>
      <div id="container">

        <etw-app-side-bar id="sidebar" .model=${this.model} aria-expanded="true"></etw-app-side-bar>

        <div id="sidebar-resize" @pointerdown=${this._sidebarSizeDown} @pointerup=${this._sidebarSizeUp}></div>

        <div id="main">
          ${!activeAgentState
            ? nothing
            : html`
              <nav class="flex bg-gray-500" @click=${this._agentTabClick}>

                <div data-tabid="agent-config" class=${classMap(agentConfigTabType)}>
                  <a href="#">Configuration</a>

                  <input type="file"
                    @change=${(e) => this._importAgentConfig(e, activeEntry)}
                    hidden />
                  <button class="ml-4 text-gray-300" @click=${(e) => this._importAgentConfigDialog(e)} title="Import Configuration">
                    <i class="fas fa-file-import"></i>
                  </button>
                  <button class="ml-2 text-gray-300" @click=${() => this._exportAgentConfig(activeAgentState)} title="Export Configuration">
                    <i class="fas fa-file-export"></i>
                  </button>

                  <button class="ml-4" @click=${() => this.model.applyAllOptions(activeAgentState)} title="Apply All">
                    <i class="fas fa-lg fa-check text-green-500"></i>
                  </button>
                  <button class="ml-2" @click=${() => this.model.resetAllOptions(activeEntry)} title="Reset All">
                    <i class="fas fa-lg fa-times text-red-500"></i>
                  </button>
                </div>

                <div data-tabid="agent-live-view" class=${classMap(agentLiveViewTabType)}>
                  <a href="#">Live View</a>
                  <button class="ml-2 ${sinkClass}" @click=${() => this._toggleEtwEvents(activeEntry)} title="View Events">
                    <i class="fas fa-eye"></i>
                  </button>
                </div>

              </nav>

              <div id="agent">
                <etw-agent .model=${this.model}
                  class="${this._tabSectionClass('agent-config')} section">
                </etw-agent>
                <live-view .model=${activeAgentState}
                  class="${this._tabSectionClass('agent-live-view')} section">
                </live-view>
            </div>
          `}
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
            <span class=${classMap(certInfoClassType)}>User certificate expires in ${window.clientCertLifeDays} days</span>
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
