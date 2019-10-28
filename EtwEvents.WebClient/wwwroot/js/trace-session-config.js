import { html } from '../lib/lit-html.js';
import { LitMvvmElement, BatchScheduler } from '../lib/@kdsoft/lit-mvvm.js';
import { repeat } from '../lib/lit-html/directives/repeat.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { css } from '../styles/css-tag.js';
import styleLinks from '../styles/kdsoft-style-links.js';
import * as utils from './utils.js';
import EventProvider from './eventProvider.js';

class TraceSessionConfig extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
  }

  connectedCallback() {
    super.connectedCallback();
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
    const evt = new CustomEvent('kdsoft-done', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model: this.model, canceled: false }
    });
    this.dispatchEvent(evt);
  }

  _profileChanged(e) {
    e.stopPropagation();
    this.model[e.target.name] = e.target.value;
    console.log(e.target);
  }

  _providerClicked(e, provider) {
    const oldExpanded = provider.expanded;
    if (!oldExpanded) {
      this.model.providers.forEach(p => { p.expanded = false; });
    }
    provider.expanded = !oldExpanded;
  }

  _addProviderClicked(e) {
    const newProvider = new EventProvider('<New Provider>', 0);
    this.model.providers.splice(0, 0, newProvider);
  }

  _deleteProviderClicked(e, provider) {
    const index = this.model.providers.findIndex(p => p.name === provider.name);
    if (index >= 0) this.model.providers.splice(index, 1);
  }

  _providerChanged(e, provider) {
    e.stopPropagation();
    provider[e.target.name] = e.target.value;
    console.log(e.target);
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

        .provider {
          display: grid;
          grid-template-columns: 1fr 2fr;
          align-items: baseline;
          grid-gap: 5px;
        }

        .provider #isDisabled {
          font-size: 1rem;
          line-height: 1.5;
        }

        #ok-cancel-buttons {
          grid-column: 1/-1;
        }
      `,
    ];
  }

  _providerTemplate(provider) {
    const expanded = provider.expanded || false;
    const borderColor = expanded ? 'border-indigo-500' : 'border-transparent';
    const htColor = expanded ? 'text-indigo-700' : 'text-gray-700';
    const timesClasses = expanded ? 'text-indigo-500 fas fa-times' : 'text-gray-500 fas fa-times';
    const chevronClasses = expanded ? 'text-indigo-500 fas fa-chevron-circle-up' : 'text-gray-500 fas fa-chevron-circle-down';
    return html`
<article class="bg-gray-100 p-2" @change=${e => this._providerChanged(e, provider)}>
  <div class="border-l-2 ${borderColor}">
    <header class="flex items-center justify-start pl-1 cursor-pointer select-none">
        <input name="name" type="text" class="${htColor} form-input mr-2 w-full" value=${provider.name} ?readonly=${!expanded} />
        <span class="${timesClasses} w-7 h-7 ml-auto mr-2" @click=${e => this._deleteProviderClicked(e, provider)}></span>
        <span class="${chevronClasses} w-7 h-7" @click=${e => this._providerClicked(e, provider)}></span>
    </header>
    <div class="mt-2" ?hidden=${!expanded}>
      <div class="provider pl-8 pb-1">
          <fieldset>
            <label class="text-gray-600" for="level">Level</label>
            <input id="level" name="level" type="number" class="form-input" value=${provider.level} />
          </fieldset>
          <fieldset>
            <label class="text-gray-600" for="keyWords">MatchKeyWords</label>
            <input id="keyWords" name="matchKeyWords" type="number" class="form-input" value=${provider.matchKeyWords} />
          </fieldset>
          <fieldset>
            <label class="text-gray-600" for="isDisabled">Disabled</label>
            <input id="isDisabled" name="disabled" type="checkbox" class="kdsoft-checkbox mt-auto mb-auto" ?checked=${provider.disabled} />
          </fieldset>
      </div>
    </div>
  </article>    `;
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
          <input id="name" name="name" type="text" class="form-input" value=${this.model.name} />
        </fieldset>
        <fieldset>
          <label for="host">Host</label>
          <input id="host" name="host" type="text" class="form-input" value=${this.model.host} />
        </fieldset>
        <fieldset>
          <label for="lifeTime">Life Time</label>
          <input id="lifeTime" name="lifeTime" type="text" class="form-input" value=${this.model.lifeTime} placeholder="Enter ISO Duration"/>
        </fieldset>
        <div id="providers" class="mt-2">
          <div class="flex mb-1 pr-2">
            <label>Providers</label>
            <span class="text-gray-500 fas fa-plus w-7 h-7 ml-auto cursor-pointer select-none" @click=${this._addProviderClicked}></span>
          </div>
          ${this.model.providers.map(provider => this._providerTemplate(provider, false))}
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
