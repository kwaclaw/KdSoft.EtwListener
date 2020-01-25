﻿import { html } from '../lib/lit-html.js';
import { LitMvvmElement, BatchScheduler } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { css } from '../styles/css-tag.js';
import sharedStyles from '../styles/kdsoft-shared-styles.js';
import './filter-carousel.js';

class FilterForm extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
  }

  _cancel(e) {
    const evt = new CustomEvent('kdsoft-done', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { canceled: true, model: this.model }
    });
    this.dispatchEvent(evt);
  }

  _apply(e) {
    const success = this.model.applyActiveFilter();
    if (success) {
      const evt = new CustomEvent('kdsoft-done', {
        // composed allows bubbling beyond shadow root
        bubbles: true, composed: true, cancelable: true, detail: { canceled: false, model: this.model }
      });
      this.dispatchEvent(evt);
    }
  }

  _test(e) {
    this.model.testActiveFilter();
  }

  _save(e) {
    this.model.postFormData();
    const evt = new CustomEvent('kdsoft-save', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model: this.model }
    });
    this.dispatchEvent(evt);
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
      ${sharedStyles}
      <style>
        :host {
          display: block;
        }
      </style>
      <filter-carousel .model=${this.model.filterCarousel}>
        <button slot="start" type="button" class="py-1 px-2" @click=${this._save}>
          <i class="fas fa-lg fa-save text-blue-500"></i>
        </button>
        <button slot="start" type="button" class="py-1 px-2" @click=${this._test}>
          <i class="fas fa-lg fa-stethoscope text-orange-500"></i>
        </button>
        <button slot="end" type="button" class="py-1 px-2" @click=${this._apply}>
          <i class="fas fa-lg fa-check text-green-500"></i>
        </button>
        <button slot="end" type="button" class="py-1 px-2" @click=${this._cancel}>
          <i class="fas fa-lg fa-times text-red-500"></i>
        </button>
      </filter-carousel>
    `;
    return result;
  }
}

window.customElements.define('filter-form', FilterForm);