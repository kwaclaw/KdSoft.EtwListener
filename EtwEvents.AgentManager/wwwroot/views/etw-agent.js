/* global i18n */

import { html, nothing } from 'lit';
import { repeat } from 'lit/directives/repeat.js';
import { LitMvvmElement, css, BatchScheduler } from '@kdsoft/lit-mvvm';
import { observable } from '@nx-js/observer-util';
import { Queue, priorities } from '@nx-js/queue-util';
import dialogPolyfill from 'dialog-polyfill';
import {
  KdSoftDropdownModel,
  KdSoftDropdownChecklistConnector,
} from '@kdsoft/lit-mvvm-components';
import checkboxStyles from '@kdsoft/lit-mvvm-components/styles/kdsoft-checkbox-styles.js';
import fontAwesomeStyles from '@kdsoft/lit-mvvm-components/styles/fontawesome/css/all-styles.js';
import tailwindStyles from '../styles/tailwind-styles.js';
import gridStyles from '../styles/kdsoft-grid-styles.js';
import dialogStyles from '../styles/dialog-polyfill-styles.js';
import '../components/etw-checklist.js';
import './etw-app-side-bar.js';
import './provider-config.js';
import './filter-edit.js';
import './event-sink-config.js';
import './live-view-config.js';
import * as utils from '../js/utils.js';

const dialogClass = utils.html5DialogSupported ? '' : 'fixed';

class EtwAgent extends LitMvvmElement {
  constructor() {
    super();
    //this.scheduler = new Queue(priorities.LOW);
    //this.scheduler = new BatchScheduler(0);
    this.scheduler = window.renderScheduler;

    // setting model property here because we cannot reliable set it from a non-lit-html rendered HTML page
    // we must assign the model *after* the scheduler, or assign it externally
    // this.model = new EtwAppModel(); --

    this.eventSinkDropDownModel = observable(new KdSoftDropdownModel());
    this.eventSinkChecklistConnector = new KdSoftDropdownChecklistConnector(
      () => this.renderRoot.getElementById('sinktype-ddown'),
      () => this.renderRoot.getElementById('sinktype-list'),
      listModel => {
        const item = listModel.firstSelectedEntry?.item;
        if (item) return `${item.sinkType} (${item.version})`;
        return '';
      }
    );
  }

  //#region Providers

  _addProviderClick(e, agentState) {
    agentState.addProvider('<New Provider>', 0);
  }

  _deleteProviderClick(e, agentState) {
    const provider = e.detail.model;
    agentState.removeProvider(provider.name);
  }

  _providerBeforeExpand(e, agentState) {
    agentState.enabledProviders.forEach(p => {
      p.expanded = false;
    });
  }

  //#endregion

  //#region Processing

  _processingFieldChange(e, agentState) {
    e.stopPropagation();
    agentState.processingModel[e.target.name] = utils.getFieldValue(e.target);
  }

  //#endregion

  //#region Event Sinks

  _addEventSinkClick() {
    const dlg = this.renderRoot.getElementById('dlg-add-event-sink');
    dlg.querySelector('form').reset();
    dlg.querySelector('#sinktype-list').model.unselectAll();
    dlg.showModal();
  }

  _okAddEventSink(e, agentState) {
    e.preventDefault();
    e.stopImmediatePropagation();
    const form = e.currentTarget.closest('form');
    const sinkname = form.querySelector('#sink-name');
    const sinklist = form.querySelector('#sinktype-list');
    const selectedSinkInfo = sinklist.model.firstSelectedEntry?.item;
    sinklist._internals.setValidity({ valueMissing: !selectedSinkInfo }, 'Sink type must be selected.');
    if (!form.reportValidity()) return;

    this.model.addEventSink(agentState, sinkname.value, selectedSinkInfo);

    form.parentElement.close();
  }

  _cancelAddEventSink(e) {
    const dlg = e.currentTarget.closest('dialog');
    dlg.close();
  }

  _deleteEventSinkClick(e, agentState) {
    const model = e.currentTarget.model;
    this.model.deleteEventSink(agentState, model.profile.name);
  }

  _eventSinkBeforeExpand(e, agentState) {
    Object.entries(agentState.eventSinks).forEach(es => {
      es[1].expanded = false;
    });
  }

  _updateEventSinks(e, agentState) {
    const configForms = this.renderRoot.querySelectorAll('event-sink-config');
    let isValid = true;
    configForms.forEach(frm => {
      isValid = isValid && frm.isValid();
    });
    if (isValid) {
      this.model.updateEventSinks(agentState);
    }
  }

  _exportEventSinks(e, agentState) {
    const profiles = Object.entries(agentState.eventSinks).map(es => es[1].profile);
    const profileString = JSON.stringify(profiles, null, 2);
    const profileURL = `data:text/plain,${profileString}`;

    const a = document.createElement('a');
    try {
      a.style.display = 'none';
      a.href = profileURL;
      a.download = `${agentState.id}_eventSinks.json`;
      document.body.appendChild(a);
      a.click();
    } finally {
      document.body.removeChild(a);
    }
  }

  _importEventSinksDialog(e) {
    const fileDlg = e.currentTarget.parentElement.querySelector('input[type="file"]');
    // reset value so that @change event fires reliably
    fileDlg.value = null;
    fileDlg.click();
  }

  _importEventSinks(e, agentState) {
    const selectedFile = e.currentTarget.files[0];
    if (!selectedFile) return;

    selectedFile.text().then(txt => {
      const profiles = JSON.parse(txt);
      const eventSinks = {};
      for (const profile of profiles) {
        eventSinks[profile.name] = { profile };
      }
      agentState.eventSinks = eventSinks;
    });
  }

  //#endregion

  //#region overrides

  /* eslint-disable indent, no-else-return */

  _addFormHandlers(frm) {
    frm.addEventListener('kdsoft-done', this._formDoneHandler);
  }

  _removeFormHandlers(frm) {
    frm.removeEventListener('kdsoft-done', this._formDoneHandler);
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    const eventSinkForm = this.renderRoot.getElementById('event-sink');
    if (eventSinkForm) this._removeFormHandlers(eventSinkForm);
  }

  shouldRender() {
    return !!this.model;
  }

  // called at most once every time after connectedCallback was executed
  static get styles() {
    return [
      tailwindStyles,
      checkboxStyles,
      fontAwesomeStyles,
      gridStyles,
      utils.html5DialogSupported ? dialogStyles : css``,
      css`
        :host {
          display: block;
          position: relative;
        }
        
        #main {
          height: 100%;
          width: auto;
          position: relative;
          //columns: 2 auto;
          display: flex;
          flex-direction: column;
          justify-content: flex-start;
          align-items: center;
          flex-wrap: wrap;
        }

        form {
          min-width:400px;
        }

        label {
          font-weight: bolder;
          color: #718096;
        }

        input {
          border-width: 1px;
        }

        #main > form {
          width: 100%;
          max-width: 600px;
          break-inside: avoid;
          margin: 0.75rem;
        }

        #sinktype-ddown {
          min-width: 12rem;
        }

        event-sink-config {
          margin-top: 10px;
          margin-bottom: 10px;
        }

        #dlg-add-event-sink form {
          display: grid;
          grid-template-columns: auto auto;
          background: rgba(255,255,255,0.3);
          row-gap: 5px;
          column-gap: 10px;
        }
      `
    ];
  }

  firstRendered() {
    const eventSinkDlg = this.renderRoot.getElementById('dlg-add-event-sink');
    if (!utils.html5DialogSupported) {
      dialogPolyfill.registerDialog(eventSinkDlg);
    }
  }

  render() {
    const activeEntry = this.model.getActiveEntry();
    const activeAgentState = this.model.activeAgentState;
    const processingModel = activeAgentState?.processingModel;
    return html`
      <div id="main">

        <form id="providers" class="border">
          <div class="flex my-2 pr-2">
            <span class="font-semibold ${this.model.getProvidersModified(activeEntry) ? 'italic text-red-500' : ''}">Event Providers</span>
            <span class="self-center text-gray-500 fas fa-lg fa-plus ml-auto cursor-pointer select-none"
              @click=${(e) => this._addProviderClick(e, activeAgentState)}>
            </span>
          </div>
          ${activeAgentState.enabledProviders.map(provider => html`
            <provider-config class="my-3"
              .model=${provider}
              @beforeExpand=${(e) => this._providerBeforeExpand(e, activeAgentState)}
              @delete=${(e) => this._deleteProviderClick(e, activeAgentState)}>
            </provider-config>
          `)}
          <hr class="my-3" />
          <div class="flex flex-wrap mt-2 bt-1">
            <button type="button" class="py-1 px-2 ml-auto" @click=${() => this.model.applyProviders(activeAgentState)} title="Apply">
              <i class="fas fa-lg fa-check text-green-500"></i>
            </button>
            <button type="button" class="py-1 px-2" @click=${() => this.model.resetProviders(activeEntry)} title="Reset to Current">
              <i class="fas fa-lg fa-times text-red-500"></i>
            </button>
          </div>
        </form>

        <form id="processing" class="max-w-full border"  @change=${e => this._processingFieldChange(e, activeAgentState)}>
          <div class="flex my-2 pr-2">
            <span class="font-semibold ${this.model.getProcessingModified(activeEntry) ? 'italic text-red-500' : ''}">Filter</span>
          </div>
          <filter-edit id="filterEdit" class="p-2" .model=${processingModel.filter}></filter-edit>
          <hr class="my-3" />
          <div class="flex flex-wrap mt-2 bt-1">
            <button type="button" class="py-1 px-2" @click=${() => this.model.testFilter(activeAgentState)} title="Test">
              <i class="fas fa-lg fa-vial" style="color:orange"></i>
            </button>
            <button type="button" class="py-1 px-2" @click=${() => this.model.clearFilter(activeAgentState)} title="Clear">
              <i class="fas fa-lg fa-ban text-gray-500"></i>
            </button>
            <button type="button" class="py-1 px-2 ml-auto" @click=${() => this.model.applyProcessingOptions(activeAgentState)} title="Apply">
              <i class="fas fa-lg fa-check text-green-500"></i>
            </button>
            <button type="button" class="py-1 px-2" @click=${() => this.model.resetProcessingOptions(activeEntry)} title="Reset to Current">
              <i class="fas fa-lg fa-times text-red-500"></i>
            </button>
          </div>
        </form> 

        <form id="event-sinks" class="border">
          <div class="flex my-2 pr-2">
            <span class="font-semibold ${this.model.getEventSinksModified(activeEntry) ? 'italic text-red-500' : ''}">Event Sinks</span>
            <span class="self-center text-gray-500 fas fa-lg fa-plus ml-auto cursor-pointer select-none"
              @click=${this._addEventSinkClick}>
            </span>
          </div>
          ${repeat(
            Object.entries(activeAgentState.eventSinks),
            entry => entry[0],
            entry => html`
              <event-sink-config class="bg-gray-300 px-2 my-3"
                .model=${entry[1]}
                @beforeExpand=${(e) => this._eventSinkBeforeExpand(e, activeAgentState)}
                @delete=${(e) => this._deleteEventSinkClick(e, activeAgentState)}>
              </event-sink-config>
            `
          )}
          <hr class="my-3" />
          <div id="ok-cancel-buttons" class="flex flex-wrap mt-2 bt-1">
            <input type="file"
              @change=${(e) => this._importEventSinks(e, activeAgentState)}
              hidden />
            <button type="button" class="mr-1 text-gray-600" @click=${(e) => this._importEventSinksDialog(e)} title="Import">
              <i class="fas fa-lg fa-file-import"></i>
            </button>
            <button type="button" class="py-1 px-2" @click=${(e) => this._exportEventSinks(e, activeAgentState)} title="Export">
              <i class="fas fa-lg fa-file-export text-gray-600"></i>
            </button>
            <button type="button" class="py-1 px-2 ml-auto" @click=${(e) => this._updateEventSinks(e, activeAgentState)} title="Apply">
              <i class="fas fa-lg fa-check text-green-500"></i>
            </button>
            <button type="button" class="py-1 px-2" @click=${() => this.model.resetEventSinks(activeEntry)} title="Reset to Current" autofocus>
              <i class="fas fa-lg fa-times text-red-500"></i>
            </button>
          </div>
        </form>

        <form id="live-view" class="border">
          <div class="flex my-2 pr-2">
            <span class="font-semibold ${this.model.getLiveViewOptionsModified(activeEntry) ? 'italic text-red-500' : ''}">Live View</span>
          </div>
          <live-view-config
            .model=${activeAgentState.liveViewConfigModel}
            .changeCallback=${(opts) => this.model.updateLiveViewOptions(activeEntry, opts)}
          ></live-view-config>
          <hr class="my-3" />
          <div class="flex flex-wrap mt-2 bt-1">
            <button type="button" class="py-1 px-2 ml-auto" @click=${() => this.model.applyLiveViewOptions(activeAgentState)} title="Apply">
              <i class="fas fa-lg fa-check text-green-500"></i>
            </button>
            <button type="button" class="py-1 px-2" @click=${() => this.model.resetLiveViewOptions(activeEntry)} title="Reset to Current">
              <i class="fas fa-lg fa-times text-red-500"></i>
            </button>
          </div>
        </form>

      </div>

      <dialog id="dlg-add-event-sink" class="${dialogClass}">
        <form name="add-event-sink" @submit=${(e) => this._okAddEventSink(e, activeAgentState)} @reset=${this._cancelAddEventSink}>
          <label for="sinktype-ddown">Name</label>
          <input id="sink-name" name="name" type="text" required />
          <label for="sinktype-ddown">Sink Type</label>
          <etw-checklist
            id="sinktype-list" 
            class="text-black" 
            .model=${this.model.sinkInfoCheckListModel}
            .getItemTemplate=${item => html`<div class="flex w-full"><span class="mr-1">${item.sinkType}</span><span class="ml-auto">(${item.version})</span></div>`}
            .attachInternals=${true}
            show-checkboxes
            required
            tabindex=-1>
          </etw-checklist>
          <span></span>
          <div class="flex flex-wrap ml-auto mt-2 bt-1">
            <button type="submit" class="py-1 px-2 ml-auto" title="Add">
              <i class="fas fa-lg fa-check text-green-500"></i>
            </button>
            <button type="reset" class="py-1 px-2" title="Cancel">
              <i class="fas fa-lg fa-times text-red-500"></i>
            </button>
          </div>
        </form>
      </dialog>
    `;
  }

  //#endregion
}

window.customElements.define('etw-agent', EtwAgent);
