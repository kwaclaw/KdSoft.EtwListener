import { html } from '../lib/lit-html.js';
import { classMap } from '../lib/lit-html/directives/class-map.js';
import { LitMvvmElement, BatchScheduler } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { css } from '../styles/css-tag.js';
import styleLinks from '../styles/kdsoft-style-links.js';
import * as utils from './utils.js';
import EventProvider from './eventProvider.js';
import './provider-config.js';
import './filter-edit.js';

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

class TraceSessionConfig extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
    this.activeTabId = 'general';
  }

  connectedCallback() {
    super.connectedCallback();
  }

  disconnectedCallback() {
    super.disconnectedCallback();
  }

  firstRendered() {
    //
  }

  rendered() {
    //
  }

  _cancel(e) {
    const evt = new CustomEvent('kdsoft-done', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model: this.model, canceled: true }
    });
    this.dispatchEvent(evt);
  }

  _apply(e) {
    const valid = this.renderRoot.querySelector('form').reportValidity();
    if (!valid) return;

    const evt = new CustomEvent('kdsoft-done', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model: this.model, canceled: false }
    });
    this.dispatchEvent(evt);
  }

  _profileChanged(e) {
    e.stopPropagation();
    this.model[e.target.name] = e.target.value;
  }

  _addProviderClicked(e) {
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

  _providerBeforeExpand(e) {
    this.model.providers.forEach(p => {
      p.expanded = false;
    });
  }

  _tabClick(e) {
    e.stopPropagation();
    const btn = e.target.closest('button');
    this.model.activeSection = btn.dataset.tabid;
  }

  _tabClass(tabId) {
    return this.model.activeSection === tabId ? classList.tabActive : classList.tabInactive;
  }

  _sectionActive(tabId) {
    return this.model.activeSection === tabId ? 'active' : '';
  }

  static get styles() {
    return [
      css`
        form {
          position: relative;
          height: 100%;
          display: flex;
          flex-direction: column;
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
        }

        #filters {
          width: 100%;
          height: 100%;
        }

        #ok-cancel-buttons {
          align-self: flex-end;
          margin-top: auto;
          width: 100%;
        }

        #name:invalid, #host:invalid, #lifeTime:invalid {
          border: 2px solid red;
        }
      `,
    ];
  }

  /* eslint-disable indent, no-else-return */

  render() {
    const result = html`
      <link rel="stylesheet" type="text/css" href=${styleLinks.tailwind} />
      <link rel="stylesheet" type="text/css" href=${styleLinks.fontawesome} />
      <link rel="stylesheet" type="text/css" href=${styleLinks.checkbox} />
      <style>
        :host {
          display: block;
        }
      </style>
      <form @change=${this._profileChanged}>
        <nav class="flex flex-col sm:flex-row mb-4" @click=${this._tabClick}>
          <button type="button" class=${classMap(this._tabClass('general'))} data-tabid="general">General</button>
          <button type="button" class=${classMap(this._tabClass('providers'))} data-tabid="providers">Providers</button>
          <button type="button" class=${classMap(this._tabClass('filters'))} data-tabid="filters">Filters</button>
          <button type="button" class=${classMap(this._tabClass('columns'))} data-tabid="columns">Columns</button>
        </nav>
        <div id="container" class="mb-4">
          <section id="general" class="${this._sectionActive('general')}">
            <fieldset>
              <label for="name">Name</label>
              <input id="name" name="name" type="text" required class="form-input" value=${this.model.name} />
            </fieldset>
            <fieldset>
              <label for="host">Host</label>
              <input id="host" name="host" type="url" class="form-input" value=${this.model.host} />
            </fieldset>
            <fieldset>
              <label for="lifeTime">Life Time</label>
              <input id="lifeTime" name="lifeTime" type="text" class="form-input"
                value=${this.model.lifeTime}
                placeholder="ISO Duration (PnYnMnDTnHnMnS)"
                pattern=${utils.isoDurationRx.source} />
            </fieldset>
          </section>
          <section id="providers" class="${this._sectionActive('providers')}">
            <div class="flex mb-1 pr-2">
              <span class="text-gray-500 fas fa-lg fa-plus w-7 h-7 ml-auto cursor-pointer select-none" @click=${this._addProviderClicked}></span>
            </div>
            ${this.model.providers.map(provider => html`
              <provider-config .model=${provider} @beforeExpand=${this._providerBeforeExpand} @delete=${this._providerDelete}></provider-config>
            `)}
          </section>
          <section id="filters" class="${this._sectionActive('filters')}">
            <filter-carousel class="h-full" .model=${this.model.filterCarousel}></filter-edit>
          </section>
          <section id="columns" class="${this._sectionActive('columns')}">
            &nbsp;
          </section>
        </div>
        <div id="ok-cancel-buttons" class="flex flex-wrap justify-end mt-2 bt-1">
          <hr class="w-full mb-4" />
          <button type="button" class="py-1 px-2" @click=${this._apply}><i class="fas fa-lg fa-check text-green-500"></i></button>
          <button type="button" class="py-1 px-2" @click=${this._cancel}><i class="fas fa-lg fa-times text-red-500"></i></button>
        </div>
      </form>
    `;
    return result;
  }
}

window.customElements.define('trace-session-config', TraceSessionConfig);
