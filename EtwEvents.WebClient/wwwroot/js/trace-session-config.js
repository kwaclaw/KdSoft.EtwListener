import { html } from '../lib/lit-html.js';
import { LitMvvmElement, BatchScheduler } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { css } from '../styles/css-tag.js';
import styleLinks from '../styles/kdsoft-style-links.js';
import * as utils from './utils.js';
import EventProvider from './eventProvider.js';
import './provider-config.js';

class TraceSessionConfig extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
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

  static get styles() {
    return [
      css`
        #container {
          display: grid;
          grid-template-columns: 1fr 2fr;
          align-items: baseline;
          grid-gap: 5px;
          min-width: 480px;
        }

        fieldset {
          display: contents;
        }

        label {
          font-weight: bolder;
        }

        #providers {
          grid-column: 1/-1;
        }

        #ok-cancel-buttons {
          grid-column: 1/-1;
        }

        #name:invalid, #host:invalid, #lifeTime:invalid {
          border: 2px solid red;
        }
      `,
    ];
  }

  render() {
    if (!this.model) return html``;

    //const filter = this.model ? this.model.filter : null;
    const result = html`
      <link rel="stylesheet" type="text/css" href=${styleLinks.tailwind} />
      <link rel="stylesheet" type="text/css" href=${styleLinks.fontawesome} />
      <link rel="stylesheet" type="text/css" href=${styleLinks.checkbox} />
      <style>
        :host {
          display: block;
        }
      </style>
      <form id="container" @change=${this._profileChanged}>
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
        <div id="providers" class="mt-2">
          <div class="flex mb-1 pr-2">
            <label>Providers</label>
            <span class="text-gray-500 fas fa-plus w-7 h-7 ml-auto cursor-pointer select-none" @click=${this._addProviderClicked}></span>
          </div>
          ${this.model.providers.map(provider => html`
            <provider-config .model=${provider} @beforeExpand=${this._providerBeforeExpand} @delete=${this._providerDelete}></provider-config>
          `)}
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
