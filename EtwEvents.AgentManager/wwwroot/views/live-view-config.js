import { Queue, priorities } from '@nx-js/queue-util';
import { observe, raw, unobserve } from '@nx-js/observer-util';
import { LitMvvmElement, html, css, BatchScheduler } from '@kdsoft/lit-mvvm';
import checkboxStyles from '../styles/kds-checkbox-styles.js';
import fontAwesomeStyles from '../styles/fontawesome/css/all-styles.js';
import appStyles from '../styles/etw-app-styles.js';
import tailwindStyles from '../styles/tailwind-styles.js';
import dialogStyles from '../styles/dialog-polyfill-styles.js';
import '../components/etw-checklist.js';
import * as utils from '../js/utils.js';
import LiveViewConfigModel from './live-view-config-model.js';

const dialogClass = utils.html5DialogSupported ? '' : 'fixed';

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
    //this.scheduler = new Queue(priorities.LOW);
    //this.scheduler = new BatchScheduler(0);
    this.scheduler = window.renderScheduler;

    this._getPayloadColumnListItemTemplate = getPayloadColumnListItemTemplate.bind(this);
    this.changeCallback = (opts) => { };
  }

  _fieldChange(e) {
    e.stopPropagation();
    this.model[e.target.name] = utils.getFieldValue(e.target);
  }

  _addPayloadColumnClick() {
    const dlg = this.renderRoot.getElementById('dlg-add-payload-col');
    dlg.querySelector('form').reset();
    dlg.showModal();
  }

  _okAddPayloadCol(e) {
    e.preventDefault();
    e.stopImmediatePropagation();
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

    const dlg = e.currentTarget.closest('dialog');
    dlg.close();
  }

  _cancelAddPayloadCol(e) {
    const dlg = e.currentTarget.closest('dialog');
    dlg.close();
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
    if (this.colObserver) {
      unobserve(this.colObserver);
      this.colObserver = null;
    }
    super.disconnectedCallback();
  }

  shouldRender() {
    return !!this.model;
  }

  beforeFirstRender() {
    // we need this to update the underlying state as early as possible
    this.colObserver = observe(() => {
      const liveViewOptions = this.model.toOptions();
      this.changeCallback(liveViewOptions);
    });
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
      utils.html5DialogSupported ? dialogStyles : css``,
      css`
        :host {
          display: block;
          position: relative;
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
          align-items: stretch;
          height: 100%;
        }

        #payload-cols {
          flex: 1;
          height: 100%;
        }

        #dlg-add-payload-col form {
          display: grid;
          grid-template-columns: auto auto;
          background: rgba(255,255,255,0.3);
          row-gap: 5px;
          column-gap: 10px;
        }
      `,
    ];
  }

  firstRendered() {
    const payloadDlg = this.renderRoot.getElementById('dlg-add-payload-col');
    if (!utils.html5DialogSupported) {
      dialogPolyfill.registerDialog(payloadDlg);
    }
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
          <div class="flex mb-1 pr-2">
            <label for="payload-cols">Payload Columns</label>
            <span class="self-center ml-auto text-gray-500 fas fa-lg fa-plus cursor-pointer select-none"
              @click=${this._addPayloadColumnClick}>
            </span>
          </div>
          <etw-checklist id="payload-cols" class="text-black"
            .model=${this.model.payloadColumnCheckList}
            .getItemTemplate=${this._getPayloadColumnListItemTemplate}
            allow-drag-drop show-checkboxes>
          </etw-checklist>
        </div>
      </div>

      <dialog id="dlg-add-payload-col" class="${dialogClass}">
        <form name="add-payload-col" @submit=${e => this._okAddPayloadCol(e)} @reset=${this._cancelAddPayloadCol}>
          <label for="payload-field">Field</label>
          <input id="payload-field" type="text" placeholder="field name" required @blur=${this._payloadFieldBlur} />
          <label for="payload-label">Label</label>
          <input id="payload-label" type="text" placeholder="field label" required />
          <label for="payload-type">Field Type</label>
            <select id="payload-type">
              ${LiveViewConfigModel.columnType.map(ct => html`<option>${ct}</option>`)}
            </select>
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
    return result;
  }
}

window.customElements.define('live-view-config', LiveViewConfig);
