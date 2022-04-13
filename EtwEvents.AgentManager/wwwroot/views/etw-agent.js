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

  _addProviderClick() {
    const activeAgentState = this.model.activeAgentState;
    if (!activeAgentState) return;
    activeAgentState.addProvider('<New Provider>', 0);
  }

  _deleteProviderClick(e) {
    const activeAgentState = this.model.activeAgentState;
    if (!activeAgentState) return;

    const provider = e.detail.model;
    activeAgentState.removeProvider(provider.name);
  }

  _providerBeforeExpand() {
    const activeAgentState = this.model.activeAgentState;
    if (!activeAgentState) return;

    activeAgentState.enabledProviders.forEach(p => {
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

  _okAddEventSink(e) {
    e.preventDefault();
    e.stopImmediatePropagation();
    const form = e.currentTarget.closest('form');
    const sinkname = form.querySelector('#sink-name');
    const sinklist = form.querySelector('#sinktype-list');
    const selectedSinkInfo = sinklist.model.firstSelectedEntry?.item;
    sinklist._internals.setValidity({ valueMissing: !selectedSinkInfo }, 'Sink type must be selected.');
    if (!form.reportValidity()) return;

    this.model.addEventSink(sinkname.value, selectedSinkInfo);

    form.parentElement.close();
  }

  _cancelAddEventSink(e) {
    const dlg = e.currentTarget.closest('dialog');
    dlg.close();
  }

  _deleteEventSinkClick(e) {
    const model = e.currentTarget.model;
    this.model.deleteEventSink(model.profile.name);
  }

  _eventSinkBeforeExpand() {
    const activeAgentState = this.model.activeAgentState;
    if (!activeAgentState) return;

    Object.entries(activeAgentState.eventSinks).forEach(es => {
      es[1].expanded = false;
    });
  }

  _updateEventSinks(e) {
    const configForms = this.renderRoot.querySelectorAll('event-sink-config');
    let isValid = true;
    configForms.forEach(frm => {
      isValid = isValid && frm.isValid();
    });
    if (isValid) {
      this.model.updateEventSinks();
    }
  }

  _exportEventSinks(e) {
    const activeAgentState = this.model.activeAgentState;
    if (!activeAgentState) return;

    const profiles = Object.entries(activeAgentState.eventSinks).map(es => es[1].profile);
    const profileString = JSON.stringify(profiles, null, 2);
    const profileURL = `data:text/plain,${profileString}`;

    const a = document.createElement('a');
    try {
      a.style.display = 'none';
      a.href = profileURL;
      a.download = `${activeAgentState.id}_eventSinks.json`;
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
          height: auto;
          width: 100%;
          position: relative;

          display: grid;
          grid-template-columns: auto auto;
          grid-gap: 1em;
          justify-items: stretch;
          overflow-y: auto;
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

        #processing {
          grid-column: 2;
          grid-row: 1 / 3;
        }

        #processingEdit {
          margin: 10px;
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
    const activeAgentState = this.model.activeAgentState;
    const processingModel = activeAgentState?.processingModel;
    return html`
      <div id="main">

        <form id="providers" class="max-w-full border">
          <div class="flex my-2 pr-2">
            <span class="font-semibold ${this.model.providersModified ? 'italic text-red-500' : ''}">Event Providers</span>
            <span class="self-center text-gray-500 fas fa-lg fa-plus ml-auto cursor-pointer select-none"
              @click=${this._addProviderClick}>
            </span>
          </div>
          ${activeAgentState.enabledProviders.map(provider => html`
            <provider-config class="my-3"
              .model=${provider}
              @beforeExpand=${this._providerBeforeExpand}
              @delete=${this._deleteProviderClick}>
            </provider-config>
          `)}
          <hr class="my-3" />
          <div class="flex flex-wrap mt-2 bt-1">
            <button type="button" class="py-1 px-2 ml-auto" @click=${() => this.model.applyProviders()} title="Apply">
              <i class="fas fa-lg fa-check text-green-500"></i>
            </button>
            <button type="button" class="py-1 px-2" @click=${() => this.model.resetProviders()} title="Reset to Current">
              <i class="fas fa-lg fa-times text-red-500"></i>
            </button>
          </div>
        </form>

        <form id="processing" class="max-w-full border"  @change=${e => this._processingFieldChange(e, activeAgentState)}>
          <div class="flex my-2 pr-2">
            <span class="font-semibold ${this.model.processingModified ? 'italic text-red-500' : ''}">Processing</span>
          </div>
          <div id="processingEdit">
            <label for="filterEdit">Filter</label>
            <filter-edit id="filterEdit" class="p-2" .model=${processingModel.filter}></filter-edit>
            <hr class="my-3" />
            <div class="flex flex-wrap mt-2 bt-1">
              <button type="button" class="py-1 px-2" @click=${() => this.model.testFilter()} title="Test">
                <i class="fas fa-lg fa-vial" style="color:orange"></i>
              </button>
              <button type="button" class="py-1 px-2" @click=${() => this.model.clearFilter()} title="Clear">
                <i class="fas fa-lg fa-ban text-gray-500"></i>
              </button>
              <button type="button" class="py-1 px-2 ml-auto" @click=${() => this.model.applyProcessing()} title="Apply">
                <i class="fas fa-lg fa-check text-green-500"></i>
              </button>
              <button type="button" class="py-1 px-2" @click=${() => this.model.resetProcessing()} title="Reset to Current">
                <i class="fas fa-lg fa-times text-red-500"></i>
              </button>
            </div>
          </div>
        </form> 

        <form id="event-sinks" class="max-w-full border">
          <div class="flex my-2 pr-2">
            <span class="font-semibold ${this.model.eventSinksModified ? 'italic text-red-500' : ''}">Event Sinks</span>
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
                @beforeExpand=${this._eventSinkBeforeExpand}
                @delete=${this._deleteEventSinkClick}>
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
            <button type="button" class="py-1 px-2" @click=${(e) => this._exportEventSinks(e)} title="Export">
              <i class="fas fa-lg fa-file-export text-gray-600"></i>
            </button>
            <button type="button" class="py-1 px-2 ml-auto" @click=${(e) => this._updateEventSinks(e)} title="Apply">
              <i class="fas fa-lg fa-check text-green-500"></i>
            </button>
            <button type="button" class="py-1 px-2" @click=${() => this.model.resetEventSinks()} title="Reset to Current" autofocus>
              <i class="fas fa-lg fa-times text-red-500"></i>
            </button>
          </div>
        </form>

        <form id="live-view" class="max-w-full border">
          <div class="flex my-2 pr-2">
            <span class="font-semibold ${this.model.liveViewConfigModified ? 'italic text-red-500' : ''}">Live View</span>
          </div>
          <live-view-config
            .model=${activeAgentState.liveViewConfigModel}
            .changeCallback=${(opts) => this.model.updateLiveViewOptions(opts)}
          ></live-view-config>
          <hr class="my-3" />
          <div class="flex flex-wrap mt-2 bt-1">
            <button type="button" class="py-1 px-2 ml-auto" @click=${() => this.model.applyLiveViewConfig()} title="Apply">
              <i class="fas fa-lg fa-check text-green-500"></i>
            </button>
            <button type="button" class="py-1 px-2" @click=${() => this.model.resetLiveViewConfig()} title="Reset to Current">
              <i class="fas fa-lg fa-times text-red-500"></i>
            </button>
          </div>
        </form>

      </div>

      <dialog id="dlg-add-event-sink" class="${dialogClass}">
        <form name="add-event-sink" @submit=${this._okAddEventSink} @reset=${this._cancelAddEventSink}>
          <label for="sinktype-ddown">Name</label>
          <input id="sink-name" name="name" type="text" required />
          <label for="sinktype-ddown">Sink Type</label>
          <etw-checklist
            id="sinktype-list" 
            class="text-black" 
            .model=${this.model.sinkInfoCheckListModel}
            .getItemTemplate=${item => html`<div class="flex w-full"><span class="mr-1">${item.sinkType}</span><span class="ml-auto">(${item.version})</span></div>`}
            .attachInternals=${true}
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
