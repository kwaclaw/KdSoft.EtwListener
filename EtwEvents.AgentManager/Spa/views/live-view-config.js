import { html } from 'lit';
import { Queue, priorities } from '@nx-js/queue-util/dist/es.es6.js';
import { LitMvvmElement, css } from '@kdsoft/lit-mvvm';
import checkboxStyles from '@kdsoft/lit-mvvm-components/styles/kdsoft-checkbox-styles.js';
import fontAwesomeStyles from '@kdsoft/lit-mvvm-components/styles/fontawesome/css/all-styles.js';
import appStyles from '../styles/etw-app-styles.js';
import tailwindStyles from '../styles/tailwind-styles.js';
import '../components/etw-checklist.js';
import * as utils from '../js/utils.js';
import LiveViewConfigModel from './live-view-config-model.js';

function getPayloadColumnListItemTemplate(item) {
  return html`
    <div class="inline-block w-1/3 mr-4 truncate" title=${item.name}>${item.name}</div>
    <div class="inline-block w-2/5 border-l pl-2 truncate" title=${item.label}>${item.label}</div>
    <div class="inline-block w-1/5 border-l pl-2" title=${item.type}>${item.type}&nbsp;</div>
    <span class="ml-auto flex-end text-gray-600 cursor-pointer" @click=${e => this._deletePayloadColumnClick(e)}>
      <i class="far fa-trash-alt"></i>
    </span>
  `;
}

class LiveViewConfig extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
    this._getPayloadColumnListItemTemplate = getPayloadColumnListItemTemplate.bind(this);
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
        :host {
          display: block;
        }

        #general {
          display: grid;
          grid-template-columns: 1fr 2fr;
          align-items: baseline;
          align-content: start;
          grid-gap: 5px;
        }

        label {
          font-weight: bolder;
          color: #718096;
        }

        #standard-cols-wrapper {
          position: relative;
        }

        #payload-cols-wrapper {
          position: relative;
          display: flex;
          flex-direction: column;
          flex-grow: 1;
          align-items: stretch;
          height: 100%;
        }

        #payload-cols {
          flex-grow: 1;
          height: 100%;
        }

        #payload-fields {
          flex-grow: 1;
          margin-top: auto;
          display: flex;
          align-items: center;
          justify-content: flex-start;
          flex-wrap: wrap;
        }
      `,
    ];
  }

  render() {
    const result = html`
      <div id="general" @change=${this._fieldChange}>
        <div id="standard-cols-wrapper" class="mr-4">
          <label class="block mb-1" for="standard-cols">Standard Columns</label>
          <etw-checklist id="standard-cols" class="w-full text-black"
            .model=${this.model.standardColumnCheckList}
            .getItemTemplate=${item => html`${item.label}`}
            allow-drag-drop show-checkboxes>
          </etw-checklist>
        </div>
        <div id="payload-cols-wrapper">
          <label class="block mb-1" for="payload-cols">Payload Columns</label>
          <etw-checklist id="payload-cols" class="text-black"
            .model=${this.model.payloadColumnCheckList}
            .getItemTemplate=${this._getPayloadColumnListItemTemplate}
            allow-drag-drop show-checkboxes>
          </etw-checklist>
          <div id="payload-fields" class="pt-4 pb-1">
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
      </div>
    `;
    return result;
  }
}

window.customElements.define('live-view-config', LiveViewConfig);
