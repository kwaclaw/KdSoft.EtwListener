import { html } from '../lib/lit-html.js';
import { LitMvvmElement, BatchScheduler } from '../lib/@kdsoft/lit-mvvm.js';
import { observable, observe } from '../lib/@nx-js/observer-util.js';
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
      bubbles: true, composed: true, cancelable: true, detail: { canceled: false, model: this.model }
    });
    this.dispatchEvent(evt);
  }

  async _apply(e) {
    const success = await this.model.applyFilter();
    if (success) {
      const evt = new CustomEvent('kdsoft-done', {
        // composed allows bubbling beyond shadow root
        bubbles: true, composed: true, cancelable: true, detail: { canceled: true, model: this.model }
      });
      this.dispatchEvent(evt);
    }
  }

  _filtersKeyDown(e) {
    const scrollDistance = e.currentTarget.clientWidth;
    switch (e.key) {
      case 'ArrowDown':
      case 'ArrowRight': {
        e.currentTarget.scrollBy({ left: scrollDistance, behavior: 'smooth' });
        // const fedit = e.target.closest('filter-edit');
        // if (!fedit) return;
        // const nextSib = fedit.nextElementSibling;
        // if (nextSib) nextSib.scrollIntoView({ behavior: 'smooth', inline: 'start' });
        break;
      }
      case 'ArrowUp':
      case 'ArrowLeft': {
        e.currentTarget.scrollBy({ left: -scrollDistance, behavior: 'smooth' });
        // const fedit = e.target.closest('filter-edit');
        // if (!fedit) return;
        // const prevSib = fedit.previousElementSibling;
        // if (prevSib) prevSib.scrollIntoView({ behavior: 'smooth', inline: 'end' });
        break;
      }
      default:
        // ignore, let bubble up
        return;
    }
    e.preventDefault();
  }

  static get styles() {
    return [
      css`
        #container {
          display: block;
        }

        #filters {
          display: flex;
          flex-wrap: nowrap;
          overflow-x: hidden;
          -webkit-overflow-scrolling: touch;
          scroll-snap-type: inline proximity;
        }

        .carousel-item {
          display: flex;
          align-items: center;
          justify-content: center;
          min-width: 100%;
          scroll-snap-align: center;
        }

        filter-edit {
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
          width: 600px;
        }
      </style>
      <div id="container">
        <ul id="filters" tabindex="0" @keydown=${this._filtersKeyDown}>
        ${this.model.editFilterModels.map((filterModel, index) => {
          // model must be an object
          return html`
            <li class="carousel-item">
              <filter-edit .model=${filterModel} tabindex="0"></filter-edit>
            </li>
          `;
        })}
        </ul>
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
