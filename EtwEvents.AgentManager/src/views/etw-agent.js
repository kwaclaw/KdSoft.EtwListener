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

  //#region Filter

  _applyFilterClick() {
    this.model.applyFilter();
  }

  _resetFilterClick() {
    this.model.resetFilter();
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
          justify-items: center;
          overflow-y: scroll;
        }

        form {
          min-width:400px;
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
    return html`
      <div id="main">
        ${activeAgentState
          ? html`
              <form id="providers" class="max-w-full border">
                <div class="flex my-2 pr-2">
                  <span class="font-semibold">Event Providers</span>
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
                  <button type="button" class="py-1 px-2" @click=${this._resetProvidersClick} title="Cancel">
                    <i class="fas fa-lg fa-times text-red-500"></i>
                  </button>
                </div>
              </form>

              <form id="filter" class="max-w-full border">
                <div class="flex my-2 pr-2">
                  <span class="font-semibold">Filter</span>
                </div>
                <filter-edit class="p-2" .model=${activeAgentState.filterModel}></filter-edit>
                <hr class="my-3" />
                <div class="flex flex-wrap mt-2 bt-1">
                  <button type="button" class="py-1 px-2" @click=${this._testFilterClick}>
                    <i class="fas fa-lg fa-stethoscope" style="color:orange"></i>
                  </button>
                  <button type="button" class="py-1 px-2 ml-auto" @click=${this._applyFilterClick} title="Apply">
                    <i class="fas fa-lg fa-check text-green-500"></i>
                  </button>
                  <button type="button" class="py-1 px-2" @click=${this._resetFilterClick} title="Cancel">
                    <i class="fas fa-lg fa-times text-red-500"></i>
                  </button>
                </div>
              </form> 

              <form id="event-sink" class="max-w-full border">
                <div class="flex my-2 pr-2">
                  <span class="font-semibold">Event Sink</span>
                  <span class="ml-auto italic text-red-500" ?hidden=${!this.model.eventSinkModified}>Modified</span>
                </div>
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
