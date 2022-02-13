import { html } from 'lit';
import { Queue, priorities } from '@nx-js/queue-util/dist/es.es6.js';
import { LitMvvmElement, css } from '@kdsoft/lit-mvvm';
import tailwindStyles from '@kdsoft/lit-mvvm-components/styles/tailwind-styles.js';
import checkboxStyles from '@kdsoft/lit-mvvm-components/styles/kdsoft-checkbox-styles.js';
import fontAwesomeStyles from '@kdsoft/lit-mvvm-components/styles/fontawesome/css/all-styles.js';
import appStyles from '../styles/etw-app-styles.js';
import * as utils from '../js/utils.js';
import LiveViewConfigModel from './live-view-config-model.js';

function getPayloadColumnListItemTemplate(item) {
  return html`
    <div class="inline-block w-1\/3 mr-4 truncate" title=${item.name}>${item.name}</div>
    <div class="inline-block w-2\/5 border-l pl-2 truncate" title=${item.label}>${item.label}</div>
    <div class="inline-block w-1\/5 border-l pl-2" title=${item.type}>${item.type}&nbsp;</div>
    <span class="ml-auto flex-end text-gray-600 cursor-pointer" @click=${e => this._deletePayloadColumnClick(e)}>
      <i class="far fa-trash-alt"></i>
    </span>
  `;
}

class LiveViewConfig extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
    this.activeTabId = 'general';
    this._getPayloadColumnListItemTemplate = getPayloadColumnListItemTemplate.bind(this);
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

  _fieldChange(e) {
    e.stopPropagation();
    this.model[e.target.name] = utils.getFieldValue(e.target);
  }

  _addPayloadColumnClick() {
    const r = this.renderRoot;
    const nameInput = r.getElementById('payload-field');
    const labelInput = r.getElementById('payload-label');
    const typeSelect = r.getElementById('payload-type');
    const valid = nameInput.reportValidity() && labelInput.reportValidity();
    if (!valid) return;

    const name = nameInput.value;
    const label = labelInput.value;
    this.model.payloadColumnCheckList.items.push({ name, label, type: typeSelect.value });
    // clear input controls
    nameInput.value = null;
    labelInput.value = null;
    typeSelect.value = 'string';
  }

  _deletePayloadColumnClick(e) {
    e.stopPropagation();
    const itemIndex = e.target.closest('.list-item').dataset.itemIndex;
    this.model.payloadColumnCheckList.items.splice(itemIndex, 1);
  }

  _payloadFieldBlur(e) {
    const fieldVal = e.currentTarget.value;
    const labelInput = this.renderRoot.getElementById('payload-label');
    if (!labelInput.value) labelInput.value = fieldVal;
  }

  /* eslint-disable indent, no-else-return */

  disconnectedCallback() {
    super.disconnectedCallback();
  }

  shouldRender() {
    return !!this.model;
  }

  firstRendered() {
    // model is defined, because of our shouldRender() override
  }

  static get styles() {
    return [
      tailwindStyles,
      checkboxStyles,
      fontAwesomeStyles,
      appStyles,
      css`
        form {
          position: relative;
          height: 100%;
          display: flex;
          flex-direction: column;
          align-items: stretch;
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

        #ok-cancel-buttons {
          margin-top: auto;
        }

        #standard-cols-wrapper {
          position: relative;
          width: 40%;
        }
      `,
    ];
  }

  render() {
    const result = html`
      <style>
        :host {
          display: block;
        }
      </style>
      <form @change=${this._fieldChange}>
        <div id="standard-cols-wrapper" class="mr-4">
          <label class="block mb-1" for="standard-cols">Standard Columns</label>
          <kdsoft-checklist id="standard-cols" class="w-full text-black"
            .model=${this.model.standardColumnCheckList}
            .getItemTemplate=${item => html`${item.label}`}
            allow-drag-drop show-checkboxes>
          </kdsoft-checklist>
        </div>
        <div id="payload-cols-wrapper" class="flex-grow flex flex-col items-stretch">
          <label class="block mb-1" for="payload-cols">Payload Columns</label>
          <kdsoft-checklist id="payload-cols" class="text-black"
            .model=${this.model.payloadColumnCheckList}
            .getItemTemplate=${this._getPayloadColumnListItemTemplate}
            allow-drag-drop show-checkboxes>
          </kdsoft-checklist>
          <div class="w-full self-end mt-auto pt-4 pb-1 flex items-center">
            <!-- <label class="mr-4" for="payload-field">New</label> -->
            <input id="payload-field" type="text" form="" class="mr-2"
              placeholder="field name" required @blur=${this._payloadFieldBlur} />
            <input id="payload-label" type="text" form="" class="mr-2" placeholder="field label" required />
            <select id="payload-type">
              ${LiveViewConfigModel.columnType.map(ct => html`<option>${ct}</option>`)}
            </select>
            <span class="text-gray-500 fas fa-lg fa-plus ml-auto pl-4 cursor-pointer select-none"
              @click=${this._addPayloadColumnClick}>
            </span>
          </div>
        </div>
        
        <hr class="mb-4" />
        <div id="ok-cancel-buttons" class="flex flex-wrap mt-2 bt-1">
          <button type="button" class="py-1 px-2 ml-auto" @click=${this._apply} title="Save">
            <i class="fas fa-lg fa-check text-green-500"></i>
          </button>
          <button type="button" class="py-1 px-2" @click=${this._cancel} title="Cancel">
            <i class="fas fa-lg fa-times text-red-500"></i>
          </button>
        </div>
      </form>
    `;
    return result;
  }
}

window.customElements.define('live-view-config', LiveViewConfig);
