/* global i18n */

import { html, nothing } from 'lit';
import { observable, observe, unobserve } from '@nx-js/observer-util';
import { Queue, priorities } from '@nx-js/queue-util';
import { LitMvvmElement, css, BatchScheduler } from '@kdsoft/lit-mvvm';
import '@kdsoft/lit-mvvm-components/kdsoft-expander.js';
import { KdSoftChecklistModel } from '@kdsoft/lit-mvvm-components';
import checkboxStyles from '@kdsoft/lit-mvvm-components/styles/kdsoft-checkbox-styles.js';
import fontAwesomeStyles from '@kdsoft/lit-mvvm-components/styles/fontawesome/css/all-styles.js';
import tailwindStyles from '../styles/tailwind-styles.js';
import spinnerStyles from '../styles/spinner-styles.js';
import appStyles from '../styles/etw-app-styles.js';
import dialogStyles from '../styles/dialog-polyfill-styles.js';
import '../components/etw-checklist.js';
import * as utils from '../js/utils.js';
import AgentState from '../js/agentState.js';
import EventProvider from '../js/eventProvider.js';

function getAgentIndex(agentList, agentId) {
  return agentList.findIndex(val => val.id === agentId);
}

class EtwAppSideBar extends LitMvvmElement {
  constructor() {
    super();
    // seems priorities.HIGH may not allow render() calls in child components in some scenarios
    //this.scheduler = new Queue(priorities.LOW);
    //this.scheduler = new BatchScheduler(0);
    this.scheduler = window.renderScheduler;
  }

  _toggleNav() {
    const host = this.renderRoot.host;
    const expanded = host.hasAttribute('aria-expanded');
    if (expanded) host.removeAttribute('aria-expanded');
    else host.setAttribute('aria-expanded', 'true');
  }

  _startEvents(agentState) {
    if (agentState && !agentState?.isRunning) this.model.startEvents();
  }

  _stopEvents(agentState) {
    if (agentState && agentState.isRunning) this.model.stopEvents();
  }

  _exportAgentConfig(agentState) {
    if (!agentState) return;

    const exportObject = new AgentState();
    utils.setTargetProperties(exportObject, agentState);

    // fix up enabled providers to exclude extra properties
    const enabledProviders = [];
    for (const provider of agentState.enabledProviders) {
      const exportProvider = new EventProvider();
      utils.setTargetProperties(exportProvider, provider);
      enabledProviders.push(exportProvider);
    }
    exportObject.enabledProviders = enabledProviders;

    for (const entry of Object.entries(agentState.eventSinks)) {
      const sinkState = entry[1];
      delete sinkState.error;
      delete sinkState.configViewUrl;
      delete sinkState.configModelUrl;
      delete sinkState.expanded;
    }

    const exportString = JSON.stringify(exportObject, null, 2);
    const exportURL = `data:text/plain,${exportString}`;

    const a = document.createElement('a');
    try {
      a.style.display = 'none';
      a.href = exportURL;
      a.download = `${exportObject.id}.json`;
      document.body.appendChild(a);
      a.click();
    } finally {
      document.body.removeChild(a);
    }
  }

  _importAgentStateDialog(e) {
    const fileDlg = e.currentTarget.parentElement.querySelector('input[type="file"]');
    // reset value so that @change event fires reliably
    fileDlg.value = null;
    fileDlg.click();
  }

  _importAgentState(e, state) {
    const selectedFile = e.currentTarget.files[0];
    if (!selectedFile) return;

    selectedFile.text().then(txt => {
      const importObject = JSON.parse(txt);
      // we don't want to change agent-identifying properties
      importObject.id = state.id;
      importObject.site = state.site;
      importObject.host = state.host;
      this.model.setAgentState(importObject);
    });
  }

  _refreshStates() {
    this.model.getState();
  }

  //#region overrides

  /* eslint-disable indent, no-else-return */

  disconnectedCallback() {
    if (this._agentListObserver) {
      unobserve(this._agentListObserver);
      this._agentListObserver = null;
    }
    super.disconnectedCallback();
  }

  shouldRender() {
    return !!this.model;
  }

  beforeFirstRender() {
    this.appTitle = this.getAttribute('appTitle');
    if (!this.agentChecklistModel) {
      const agentList = this.model.agents;
      const agentIndex = getAgentIndex(agentList, this.model.activeAgentId);
      const checklistModel = new KdSoftChecklistModel(
        agentList,
        agentIndex >= 0 ? [agentIndex] : [],
        false,
        item => item.id
      );
      this.agentChecklistModel = observable(checklistModel);

      this._agentListObserver = observe(() => {
        const oldActiveAgentId = this.model.activeAgentId;
        const selEntry = this.agentChecklistModel.firstSelectedEntry;
        const selAgent = selEntry?.item;
        this.model.activeAgentId = selAgent?.state.id;
        if (oldActiveAgentId != this.model.activeAgentId) {
          this.model.stopEtwEvents();
        }
      });
    }
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
      spinnerStyles,
      utils.html5DialogSupported ? dialogStyles : css``,
      css`
        :host {
          display: block;
          position: relative;
        }

        dialog {
          outline: lightgray solid 1px;
        }
        
        #sidebar {
          display: flex;
          flex-direction: column;
          flex-wrap: nowrap;
          justify-content: flex-start;
          align-items: stretch;
          width: 100%;
          min-width: 8rem;
        }

        #nav-toggle {
          position: fixed;
          top:0;
          left: 0;
          display: flex;
          align-items: center;
          background-color: transparent;
          z-index: 30;
          height: 2em;
          width: 2em;
        }

        #nav-toggle:focus {
          outline: none;
        }

        :host([aria-expanded]) #nav-toggle {
          color: #a0aec0;
          border-color: #718096;
        }
        
        :host([aria-expanded]) #nav-toggle:hover {
          color: white;
          border-color: white;
        }
        
        .brand {
          font-family: Candara;
        }

        /* the item template for the checklist contains a part we can select */ 
        #agents::part(content) {
          display: grid;
          grid-gap: 0 1em;
          grid-template-columns: max-content auto;
          justify-items: start;
        }

        #agents::part(header) {
          color: gray;
          background-color: inherit;
        }

        #agents::part(header):hover {
          background-color: inherit;
        }

        #agents::part(header).item-selected {
          background-color: inherit;
        }

        .fa-lg.fa-eye, .fa-lg.fa-file-archive {
          min-width: 2em;
        }
      `
    ];
  }

  getAgentTemplate(entry) {
    const onlyModified = entry.modified && !entry.disconnected;
    const playClass = entry.current?.isRunning ? '' : 'text-green-500';
    const stopClass = entry.current?.isRunning ? 'text-red-500' : '';
    return html`
      <kdsoft-expander class="w-full" .scheduler=${this.scheduler}>
        <div part="header" slot="header" class="flex items-baseline pr-1 text-white bg-gray-500">
          <label class="pl-1 font-bold text-xl">${entry.state.id}</label>
          <span class="ml-auto">
            <span class="mr-4">
              ${onlyModified ? html`<button class="mr-1 text-yellow-800 fas fa-pencil-alt"></button>` : nothing}
              ${entry.disconnected ? html`<i class="text-red-800 fas fa-unlink"></i>` : nothing}
            </span>

            <input type="file"
              @change=${(e) => this._importAgentState(e, entry.state)}
              hidden />
            <button class="mr-1 text-gray-600" @click=${(e) => this._importAgentStateDialog(e)} title="Import Configuration">
              <i class="fas fa-file-import"></i>
            </button>
            <button class="mr-4 text-gray-600" @click=${() => this._exportAgentConfig(entry.state)} title="Export Configuration">
              <i class="fas fa-file-export"></i>
            </button>

            <button class="mr-1 ${playClass}" @click=${() => this._startEvents(entry.current)} title="Start Session">
              <i class="fas fa-play"></i>
            </button>
            <button class="mr-1 ${stopClass}" @click=${() => this._stopEvents(entry.current)} title="Stop Session">
              <i class="fas fa-stop"></i>
            </button>
          </span>
        </div>
        <!-- using part="slot" we can style this from here even though it will be rendered inside a web component -->
        <div part="content" slot="content" class="pl-3">
          <label class="pl-1 font-bold">Site</label>
          <div class="pl-1">${entry.state.site}</div>
          <label class="pl-1 font-bold">Host</label>
          <div class="pl-1">${entry.state.host}</div>
        </div>
      </kdsoft-expander>
    `;
  }

  render() {
    return html`
      <nav id="sidebar" class="text-gray-500 bg-gray-800 pt-0 pb-3 h-full z-30">
        <!-- <div class="pr-2"> -->
          <button id="nav-toggle"
            @click=${this._toggleNav}
            class="text-gray-600 border-gray-600 hover:text-gray-800"
          >
            <i class="m-auto fas fa-lg fa-bars"></i>
          </button>
        <!-- </div> -->
        <div class="flex pl-8 pr-2">
          <!-- <div class="flex items-center flex-shrink-0 text-white mr-6"> -->
            <a class="text-white no-underline hover:text-white hover:no-underline" href="#">
              <span class="text-2xl pl-2 brand"><i class="brand"></i>KDS</span>
            </a>
            <button class="text-blue-500 ml-auto fas fa-redo-alt" @click=${() => this._refreshStates()}></button>
          <!-- </div> -->
        </div>

        <etw-checklist id="agents" class="text-black"
          .model=${this.agentChecklistModel}
          .scheduler=${this.scheduler}
          .getItemTemplate=${entry => this.getAgentTemplate(entry)}
        ></etw-checklist>

      </nav>
    `;
  }

  //#endregion
}

window.customElements.define('etw-app-side-bar', EtwAppSideBar);
