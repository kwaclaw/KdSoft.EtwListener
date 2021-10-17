/* global i18n */

import { html, nothing } from 'lit';
import { observable, observe } from '@nx-js/observer-util/dist/es.es6.js';
import { Queue, priorities } from '@nx-js/queue-util/dist/es.es6.js';
import { LitMvvmElement, css } from '@kdsoft/lit-mvvm';
import { KdSoftChecklistModel } from '@kdsoft/lit-mvvm-components';
import tailwindStyles from '@kdsoft/lit-mvvm-components/styles/tailwind-styles.js';
import checkboxStyles from '@kdsoft/lit-mvvm-components/styles/kdsoft-checkbox-styles.js';
import fontAwesomeStyles from '@kdsoft/lit-mvvm-components/styles/fontawesome/css/all-styles.js';
import spinnerStyles from '../styles/spinner-styles.js';
import appStyles from '../styles/etw-app-styles.js';
import dialogStyles from '../styles/dialog-polyfill-styles.js';
import * as utils from '../js/utils.js';

function getAgentIndex(agentList, agentId) {
  return agentList.findIndex(val => val.id === agentId);
}

class EtwAppSideBar extends LitMvvmElement {
  constructor() {
    super();
    // seems priorities.HIGH may not allow render() calls in child components in some scenarios
    this.scheduler = new Queue(priorities.LOW);
  }

  _toggleNav() {
    const host = this.renderRoot.host;
    const expanded = host.hasAttribute('aria-expanded');
    if (expanded) host.removeAttribute('aria-expanded');
    else host.setAttribute('aria-expanded', 'true');
  }

  _startEvents(agentState) {
    if (!agentState.isRunning) this.model.startEvents();
  }

  _stopEvents(agentState) {
    if (agentState.isRunning) this.model.stopEvents();
  }

  _refreshStates() {
    this.model.getAgentStates();
  }

  //#region overrides

  /* eslint-disable indent, no-else-return */

  disconnectedCallback() {
    super.disconnectedCallback();
  }

  shouldRender() {
    return !!this.model;
  }

  // called at most once every time after connectedCallback was executed
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
        const selEntry = this.agentChecklistModel.firstSelectedEntry;
        const selAgent = selEntry?.item;
        this.model.activeAgentId = selAgent?.state.id;
      });
    }
  }

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

        #dlg-config {
          width: 800px;
          min-height: 400px;
          height: 500px;
          max-height: 600px;
        }
        
        #dlg-filter {
          width: 80ch;
        }

        #dlg-event-sink {
          width: 800px;
          min-height: 400px;
          /* height: 500px; */
          max-height: 800px;
        }

        #dlg-event-sink-chooser {
          color: inherit;
          background-color: #718096;
          left: unset;
          margin: 0;
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
    const playClass = entry.current.isRunning ? '' : 'text-green-500';
    const stopClass = entry.current.isRunning ? 'text-red-500' : '';
    return html`
      <kdsoft-expander class="w-full" .scheduler=${this.scheduler}>
        <div part="header" slot="header" class="flex items-baseline pr-1 text-white bg-gray-500">
          <label class="pl-1 font-bold text-xl">${entry.state.id}</label>
          <span class="ml-auto">
            ${onlyModified ? html`<button class="mr-1 text-yellow-800 fas fa-pencil-alt"></button>` : nothing}
            ${entry.disconnected ? html`<i class="mr-1 text-red-800 fas fa-unlink"></i>` : nothing}
            <button class="mr-1 ${playClass} fas fa-play" @click=${() => this._startEvents(entry.current)}></button>
            <button class="mr-1 ${stopClass} fas fa-stop" @click=${() => this._stopEvents(entry.current)}></button>
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
      <style>
        :host {
          position: relative;
        }
      </style>
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

        <kdsoft-checklist id="agents" class="text-black"
          .model=${this.agentChecklistModel}
          .scheduler=${this.scheduler}
          .getItemTemplate=${entry => this.getAgentTemplate(entry)}
        ></kdsoft-checklist>

      </nav>
    `;
  }

  //#endregion
}

window.customElements.define('etw-app-side-bar', EtwAppSideBar);
