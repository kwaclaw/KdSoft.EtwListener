/* global i18n */

import { html, nothing } from 'lit';
import { Queue, priorities } from '@nx-js/queue-util/dist/es.es6.js';
import { LitMvvmElement, css } from '@kdsoft/lit-mvvm';
import './etw-app-side-bar.js';
import './provider-config.js';
import './filter-edit.js';
import './event-sink-config.js';
import tailwindStyles from '@kdsoft/lit-mvvm-components/styles/tailwind-styles.js';
import checkboxStyles from '@kdsoft/lit-mvvm-components/styles/kdsoft-checkbox-styles.js';
import fontAwesomeStyles from '@kdsoft/lit-mvvm-components/styles/fontawesome/css/all-styles.js';
import gridStyles from '../styles/kdsoft-grid-styles.js';
import * as utils from '../js/utils.js';

function formDoneHandler(e) {
  if (e.target.localName === 'event-sink-config') {
    if (e.detail.canceled) {
      this.model.resetEventSink();
    } else {
      this.model.updateEventSink();
    }
  }
}

class EtwAgent extends LitMvvmElement {
  constructor() {
    super();

    // setting model property here because we cannot reliable set it from a non-lit-html rendered HTML page
    this.scheduler = new Queue(priorities.HIGH);
    // we must assign the model *after* the scheduler, or assign it externally
    // this.model = new EtwAppModel(); --

    this._formDoneHandler = formDoneHandler.bind(this);
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

  _applyProvidersClick() {
    this.model.applyProviders();
  }

  _resetProvidersClick() {
    this.model.resetProviders();
  }

  //#endregion

  //#region Processing

  _processingFieldChange(e, agentState) {
    e.stopPropagation();
    agentState.processingModel[e.target.name] = utils.getFieldValue(e.target);
  }

  _applyProcessingClick() {
    this.model.applyProcessing();
  }

  _resetProcessingClick() {
    this.model.resetProcessing();
  }

  _clearFilterClick() {
    this.model.clearFilter();
  }

  _testFilterClick() {
    this.model.testFilter();
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
      css`
        :host {
          display: block;
          position: relative;
        }
        
        #main {
          grid-column: 3;
          grid-row: 1/2;
          height: auto;
          width: 100%;
          position: relative;

          display: grid;
          grid-template-columns: auto auto;
          grid-gap: 1em;
          justify-items: stretch;
          overflow-y: scroll;
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

        #processingVars {
          display: grid;
          grid-template-columns: auto auto;
          background: rgba(255,255,255,0.3);
          row-gap: 5px;
          column-gap: 10px;
          margin-bottom: 10px;
        }

        event-sink-config {
          margin: 10px;
        }
      `
    ];
  }

  rendered() {
    const eventSinkForm = this.renderRoot.getElementById('event-sink');
    if (eventSinkForm) {
      this._addFormHandlers(eventSinkForm);
    }
  }

  render() {
    const activeAgentState = this.model.activeAgentState;
    const processingModel = activeAgentState?.processingModel;
    return html`
      <div id="main">
        ${activeAgentState
          ? html`
              <form id="providers" class="max-w-full border">
                <div class="flex my-2 pr-2">
                  <span class="font-semibold ${this.model.providersModified ? 'italic text-red-500' : ''}">Event Providers</span>
                  <span class="self-center text-gray-500 fas fa-lg fa-plus ml-auto cursor-pointer select-none"
                    @click=${this._addProviderClick}>
                  </span>
                </div>
                ${activeAgentState.enabledProviders.map(provider => html`
                  <provider-config
                    .model=${provider}
                    @beforeExpand=${this._providerBeforeExpand}
                    @delete=${this._deleteProviderClick}>
                  </provider-config>
                `)}
                <hr class="my-3" />
                <div class="flex flex-wrap mt-2 bt-1">
                  <button type="button" class="py-1 px-2 ml-auto" @click=${this._applyProvidersClick} title="Apply">
                    <i class="fas fa-lg fa-check text-green-500"></i>
                  </button>
                  <button type="button" class="py-1 px-2" @click=${this._resetProvidersClick} title="Reset to Current">
                    <i class="fas fa-lg fa-times text-red-500"></i>
                  </button>
                </div>
              </form>

              <form id="processing" class="max-w-full border"  @change=${e => this._processingFieldChange(e, activeAgentState)}>
                <div class="flex my-2 pr-2">
                  <span class="font-semibold ${this.model.processingModified ? 'italic text-red-500' : ''}">Processing</span>
                </div>
                <div id="processingEdit">
                  <div id="processingVars">
                    <label for="batchSize">Batch Size</label>
                    <input type="number" id="batchSize" name="batchSize" .value=${processingModel.batchSize} />
                    <label for="maxWriteDelayMSecs">Max Write Delay (msecs)</label>
                    <input type="number" id="maxWriteDelayMSecs" name="maxWriteDelayMSecs" .value=${processingModel.maxWriteDelayMSecs} />
                  </div>
                  <label for="filterEdit">Filter</label>
                  <filter-edit id="filterEdit" class="p-2" .model=${processingModel.filter}></filter-edit>
                  <hr class="my-3" />
                  <div class="flex flex-wrap mt-2 bt-1">
                    <button type="button" class="py-1 px-2" @click=${this._testFilterClick} title="Test">
                      <i class="fas fa-lg fa-stethoscope" style="color:orange"></i>
                    </button>
                    <button type="button" class="py-1 px-2" @click=${this._clearFilterClick} title="Clear">
                      <i class="fas fa-lg fa-ban text-gray-500"></i>
                    </button>
                    <button type="button" class="py-1 px-2 ml-auto" @click=${this._applyProcessingClick} title="Apply">
                      <i class="fas fa-lg fa-check text-green-500"></i>
                    </button>
                    <button type="button" class="py-1 px-2" @click=${this._resetProcessingClick} title="Reset to Current">
                      <i class="fas fa-lg fa-times text-red-500"></i>
                    </button>
                  </div>
                </div>
              </form> 

              <form id="event-sink" class="max-w-full border">
                <div class="flex my-2 pr-2">
                  <span class="font-semibold ${this.model.eventSinkModified ? 'italic text-red-500' : ''}">Event Sink</span>
                </div>
                <pre ?hidden=${!this.model.eventSinkError}><textarea 
                    class="my-2 w-full border-2 border-red-500 focus:outline-none focus:border-red-700"
                  >${this.model.eventSinkError}</textarea></pre>
                <event-sink-config .model=${activeAgentState.sinkConfigModel}></event-sink-config>
              </form>
            `
          : nothing
        }
      </div>
    `;
  }

  //#endregion
}

window.customElements.define('etw-agent', EtwAgent);
