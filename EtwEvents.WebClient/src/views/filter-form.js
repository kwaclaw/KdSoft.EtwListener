import { html, nothing } from 'lit';
import { Queue, priorities } from '@nx-js/queue-util/dist/es.es6.js';
import { LitMvvmElement, css } from '@kdsoft/lit-mvvm';
import './filter-carousel.js';
import Spinner from '../js/spinner.js';
import tailwindStyles from '@kdsoft/lit-mvvm-components/styles/tailwind-styles.js';
import checkboxStyles from '@kdsoft/lit-mvvm-components/styles/kdsoft-checkbox-styles.js';
import fontAwesomeStyles from '@kdsoft/lit-mvvm-components/styles/fontawesome/css/all-styles.js';
import spinnerStyles from '../styles/spinner-styles.js';
import appStyles from '../styles/etw-app-styles.js';

class FilterForm extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
  }

  _cancel() {
    const evt = new CustomEvent('kdsoft-done', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { canceled: true, model: this.model }
    });
    this.dispatchEvent(evt);
  }

  _apply(e) {
    const spinner = new Spinner(e.currentTarget);
    const success = this.model.applyActiveFilter(spinner);
    if (success) {
      const evt = new CustomEvent('kdsoft-done', {
        // composed allows bubbling beyond shadow root
        bubbles: true, composed: true, cancelable: true, detail: { canceled: false, model: this.model }
      });
      this.dispatchEvent(evt);
    }
  }

  _test(e) {
    const spinner = new Spinner(e.currentTarget);
    this.model.testActiveFilter(spinner);
  }

  _save() {
    this.model.postFormData();
    const evt = new CustomEvent('kdsoft-save', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model: this.model }
    });
    this.dispatchEvent(evt);
  }

  shouldRender() {
    return !!this.model;
  }

  static get styles() {
    return [
      tailwindStyles,
      checkboxStyles,
      fontAwesomeStyles,
      appStyles,
      spinnerStyles,
      css`
        #container {
          display: block;
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
