import { html, nothing } from '../lib/lit-html.js';
import { repeat } from '../lib/lit-html/directives/repeat.js';
import { LitMvvmElement, css } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util/dist/es.es6.js';
import { observable, observe, unobserve, raw } from '../lib/@nx-js/observer-util/dist/es.es6.js';
import * as utils from '../js/utils.js';
import sharedStyles from '../styles/kdsoft-shared-styles.js';
import styleLinks from '../styles/kdsoft-style-links.js';
import '../components/kdsoft-dropdown.js';
import '../components/kdsoft-checklist.js';
import '../components/kdsoft-expander.js';
import '../components/kdsoft-drop-target.js';
import '../components/kdsoft-tree-view.js';
import EventSinkConfigModel from './event-sink-config-model.js';
import KdSoftDropdownModel from '../components/kdsoft-dropdown-model.js';
import KdSoftDropdownChecklistConnector from '../components/kdsoft-dropdown-checklist-connector.js';


function getSelectedSinkTypeText(checkListModel) {
  let result = null;
  for (const selEntry of checkListModel.selectedEntries) {
    if (result) result += `, ${selEntry.item.name}`;
    else result = selEntry.item.name;
  }
  return result;
}

class EventSinkConfig extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
    // for "nothing" to work we need to render raw(this.sinkTypeTemplateHolder.value)
    this.sinkTypeTemplateHolder = observable({ value: nothing });
  }

  _cancel() {
    const evt = new CustomEvent('kdsoft-done', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model: this.model, canceled: true }
    });
    this.dispatchEvent(evt);
  }

  _apply() {
    const valid = this.renderRoot.querySelector('form').reportValidity();
    if (!valid) return;

    const evt = new CustomEvent('kdsoft-done', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model: this.model, canceled: false }
    });
    this.dispatchEvent(evt);
  }

  _export() {
    this.model.export();
  }

  /* eslint-disable indent, no-else-return */

  connectedCallback() {
    super.connectedCallback();
    // this.addEventListener('kdsoft-node-move', this.rootNode.moveNode);
  }

  disconnectedCallback() {
    // this.removeEventListener('kdsoft-node-move', this.rootNode.moveNode);
    super.disconnectedCallback();
  }

  shouldRender() {
    const result = !!this.model;
    return result;
  }

  beforeFirstRender() {
    // model is defined, because of our shouldRender() override
  }

  firstRendered() {
    //
    this._sinkTypeObserver = observe(async () => {
      if (this.model.selectedSinkTypeIndex >= 0) {
        const href = EventSinkConfigModel.sinkTypeList[this.model.selectedSinkTypeIndex].href;
        try {
          const module = await import(href);
          this.sinkTypeTemplateHolder.value = module.default;
        } catch(error) {
          // for "nothing" to work we need to render raw(this.sinkTypeTemplateHolder.value)
          this.sinkTypeTemplateHolder.value = nothing;
          window.etwApp.defaultHandleError(error);
        }
      }
    });
  }

  rendered() {
    //
  }

  static get styles() {
    return [
      css`
        form {
          position: relative;
          height: 100%;
          display: flex;
          flex-direction: column;
          align-items: stretch;
        }

        #form-content {
          position: relative;
          flex-grow: 1;
        }

        #select-grid {
          display:grid;
          position:absolute;
          left:0;
          top:0;
          right:0;
          bottom:0;
          /* only way to have background, but not children, semi-transparent */
          background: rgba(255,255,255,0.3);
          z-index:999;
        }

        #select-grid div {
          margin:auto;
          max-height:100%;
          max-width:100%;
          min-width: 12rem;
          z-index:1000;
        }

        #select-grid kdsoft-checklist {
          width: 100%;
        }

        #container {
          position: relative;
          flex: 1 1 auto;
          overflow-y: auto;
        }

        section {
          position: relative;
        }

        section:not(.active) {
          display: none !important;
        }

        #general {
          display: grid;
          grid-template-columns: 1fr 2fr;
          align-items: baseline;
          align-content: start;
          grid-gap: 5px;
          min-width: 480px;
        }

        fieldset {
          display: contents;
        }

        label {
          font-weight: bolder;
          color: #718096;
        }

        #filters {
          height: 100%;
        }

        #ok-cancel-buttons {
          margin-top: auto;
        }

        #name:invalid, #host:invalid, #lifeTime:invalid {
          border: 2px solid red;
        }

        #standard-cols-wrapper {
          position: relative;
          width: 40%;
        }

      `,
    ];
  }

  getSelectTemplate(alreadySelected) {
    if (alreadySelected) {
      return nothing;
    }
    return html`
      <div id="select-grid">
        <div>
          <p>Select Event Sink Type</p>
          <kdsoft-checklist
            id="sinkTypeList" 
            class="text-black" 
            .model=${this.model.sinkTypeCheckListModel}
            .getItemTemplate=${item => html`${item.name}`}>
          </kdsoft-checklist>
        </div>
      </div>
    `;
  }

  render() {
    const result = html`
      ${sharedStyles}
      <link rel="stylesheet" type="text/css" href=${styleLinks.checkbox} />
      <style>
        :host {
          display: block;
          height:100%;
        }
      </style>
      <form>
        <div id="form-content">
          ${this.getSelectTemplate(this.model.selectedSinkTypeIndex >= 0)}
          <div id="container" class="mb-4 relative">
            ${raw(this.sinkTypeTemplateHolder.value)}
          </div>
        </div>

        <hr class="mb-4" />
        <div id="ok-cancel-buttons" class="flex flex-wrap mt-2 bt-1">
          <button type="button" class="py-1 px-2" @click=${this._export} title="Export">
            <i class="fas fa-lg fa-file-export text-gray-600"></i>
          </button>
          <button type="button" class="py-1 px-2 ml-auto" @click=${this._apply} title="Save">
            <i class="fas fa-lg fa-check text-green-500"></i>
          </button>
          <button type="button" class="py-1 px-2" @click=${this._cancel} title="Cancel" autofocus>
            <i class="fas fa-lg fa-times text-red-500"></i>
          </button>
        </div>
      </form>
    `;
    return result;
  }
}

window.customElements.define('event-sink-config', EventSinkConfig);
