import { html } from '../lib/lit-html.js';
import { LitMvvmElement, BatchScheduler } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { css } from '../styles/css-tag.js';
import styleLinks from '../styles/kdsoft-style-links.js';
import './filter-edit.js';

class FilterForm extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
  }

  _cancel(e) {
    const evt = new CustomEvent('kdsoft-done', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { saved: false, component: this }
    });
    this.dispatchEvent(evt);
  }

  async _apply(e) {
    const success = await this.model.applyFilter();
    if (success) {
      const evt = new CustomEvent('kdsoft-done', {
        // composed allows bubbling beyond shadow root
        bubbles: true, composed: true, cancelable: true, detail: { saved: true, component: this }
      });
      this.dispatchEvent(evt);
    }
  }

  static get styles() {
    return [
      css`
        #container {
          display: block;
        }
      `,
    ];
  }

  render() {
    const result = html`
      <link rel="stylesheet" type="text/css" href=${styleLinks.tailwind} />
      <link rel="stylesheet" type="text/css" href=${styleLinks.fontawesome} />
      <style>
        :host {
          display: block;
        }
      </style>
      <div id="container">
        <filter-edit .model=${this.model}></filter-edit>
        <div class="flex justify-end mt-2">
          <button type="button" class="py-1 px-2" @click=${this._apply}><i class="fas fa-lg fa-check text-green-500"></i></button>
          <button type="button" class="py-1 px-2" @click=${this._cancel}><i class="fas fa-lg fa-times text-red-500"></i></button>
        </div>
      </div>
    `;
    return result;
  }
}

window.customElements.define('filter-form', FilterForm);
