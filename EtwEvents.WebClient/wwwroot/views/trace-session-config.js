import { html } from '../lib/lit-html.js';
import { classMap } from '../lib/lit-html/directives/class-map.js';
import { LitMvvmElement, css } from '../lib/@kdsoft/lit-mvvm.js';
import { observe, unobserve } from '../lib/@nx-js/observer-util/dist/es.es6.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util/dist/es.es6.js';
import sharedStyles from '../styles/kdsoft-shared-styles.js';
import styleLinks from '../styles/kdsoft-style-links.js';
import * as utils from '../js/utils.js';
import EventProvider from '../js/eventProvider.js';
import './provider-config.js';
import './filter-edit.js';
import TraceSessionProfile from '../js/traceSessionProfile.js';

const tabBase = {
  'text-gray-600': true,
  'pt-4': true,
  'pb-2': true,
  'px-6': true,
  block: true,
  'hover:text-blue-500': true,
  'focus:outline-none': true
};

const classList = {
  tabActive: { ...tabBase, 'text-blue-500': true, 'border-b-2': true, 'font-medium': true, 'border-blue-500': true },
  tabInactive: tabBase,
};

function cl(key) {
  return classMap(classList[key]);
}

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

class TraceSessionConfig extends LitMvvmElement {
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

  _exportProfile() {
    this.model.exportProfile();
  }

  _profileChange(e) {
    e.stopPropagation();
    this.model[e.target.name] = utils.getFieldValue(e.target);
  }

  _addProviderClick() {
    const newProvider = new EventProvider('<New Provider>', 0);
    this.model.providers.splice(0, 0, newProvider);
    this.model.providers.forEach(p => {
      p.expanded = false;
    });
    newProvider.expanded = true;
  }

  _providerDelete(e) {
    const provider = e.detail.model;
    const index = this.model.providers.findIndex(p => p.name === provider.name);
    if (index >= 0) this.model.providers.splice(index, 1);
  }

  _providerBeforeExpand() {
    this.model.providers.forEach(p => {
      p.expanded = false;
    });
  }

  _tabClick(e) {
    e.stopPropagation();
    const btn = e.target.closest('button');
    if (!btn) return;
    this.model.activeSection = btn.dataset.tabid;
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

  _tabClass(tabId) {
    return cl(this.model.activeSection === tabId ? 'tabActive' : 'tabInactive');
  }

  _sectionClass(tabId) {
    return this.model.activeSection === tabId ? 'active' : '';
  }

  /* eslint-disable indent, no-else-return */

  disconnectedCallback() {
    super.disconnectedCallback();
    if (this._filterObserver) unobserve(this._filterObserver);
  }

  shouldRender() {
    return !!this.model;
  }

  firstRendered() {
    // model is defined, because of our shouldRender() override
    this._filterObserver = observe(() => {
      this.model.filters = this.model.filterCarousel.filterModels.map(fm => fm.filter);
      this.model.activeFilterIndex = this.model.filterCarousel.activeFilterIndex;
    });
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

  render() {
    const result = html`
      ${sharedStyles}
      <link rel="stylesheet" type="text/css" href=${styleLinks.checkbox} />
      <style>
        :host {
          display: block;
        }
      </style>
      <form @change=${this._profileChange}>
        <nav class="flex flex-col sm:flex-row mb-4" @click=${this._tabClick}>
          <button type="button" class=${this._tabClass('general')} data-tabid="general">General</button>
          <button type="button" class=${this._tabClass('providers')} data-tabid="providers">Providers</button>
          <button type="button" class=${this._tabClass('filters')} data-tabid="filters">Filters</button>
          <button type="button" class=${this._tabClass('columns')} data-tabid="columns">Columns</button>
        </nav>

        <div id="container" class="mb-4">

          <section id="general" class="${this._sectionClass('general')}">
            <fieldset>
              <label for="name">Name</label>
              <div class="flex flex-col">
                <input id="name" name="name" type="text" required .value=${this.model.name} />
                <span class="text-gray-600 text-sm italic">Modifying the name creates a clone of the current profile.</span>
              </div>
            </fieldset>
            <fieldset>
              <label for="host">Host</label>
              <input id="host" name="host" type="url" .value=${this.model.host} />
            </fieldset>
            <fieldset>
              <label for="lifeTime">Life Time</label>
              <input id="lifeTime" name="lifeTime" type="text"
                .value=${this.model.lifeTime}
                placeholder="ISO Duration (PnYnMnDTnHnMnS)"
                pattern=${utils.isoDurationRx.source} />
            </fieldset>
          </section>

          <section id="providers" class="${this._sectionClass('providers')}">
            <div class="flex my-2 pr-2">
              <span class="self-center text-gray-500 fas fa-lg fa-plus ml-auto cursor-pointer select-none"
                @click=${this._addProviderClick}></span>
            </div>
            ${this.model.providers.map(provider => html`
              <provider-config
                .model=${provider}
                @beforeExpand=${this._providerBeforeExpand}
                @delete=${this._providerDelete}>
              </provider-config>
            `)}
          </section>

          <section id="filters" class="${this._sectionClass('filters')}">
            <filter-carousel class="h-full" .model=${this.model.filterCarousel}></filter-carousel>
          </section>

          <section id="columns" class="${this._sectionClass('columns')} h-full flex items-stretch">
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
                <input id="payload-field" type="text" form="" class="mr-2" placeholder="field name" required @blur=${this._payloadFieldBlur} />
                <input id="payload-label" type="text" form="" class="mr-2" placeholder="field label" required />
                <select id="payload-type">
                  ${TraceSessionProfile.columnType.map(ct => html`<option>${ct}</option>`)}
                </select>
                <span class="text-gray-500 fas fa-lg fa-plus ml-auto pl-4 cursor-pointer select-none" @click=${this._addPayloadColumnClick}></span>
              </div>
            </div>
          </section>

        </div>
        
        <hr class="mb-4" />
        <div id="ok-cancel-buttons" class="flex flex-wrap mt-2 bt-1">
          <button type="button" class="py-1 px-2" @click=${this._exportProfile} title="Export"><i class="fas fa-lg fa-file-export text-gray-600"></i></button>
          <button type="button" class="py-1 px-2 ml-auto" @click=${this._apply} title="Save"><i class="fas fa-lg fa-check text-green-500"></i></button>
          <button type="button" class="py-1 px-2" @click=${this._cancel} title="Cancel"><i class="fas fa-lg fa-times text-red-500"></i></button>
        </div>
      </form>
    `;
    return result;
  }
}

window.customElements.define('trace-session-config', TraceSessionConfig);
