import { html } from '../lib/lit-html.js';
import { LitMvvmElement, BatchScheduler } from '../lib/@kdsoft/lit-mvvm.js';
import { observable, observe, unobserve } from '../lib/@nx-js/observer-util.js';
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
      bubbles: true, composed: true, cancelable: true, detail: { canceled: true, model: this.model }
    });
    this.dispatchEvent(evt);
  }

  //TODO we should have these operations: save, apply & save, cancel
  // and they should copy the filters to the profile before applying and/or saving
  async _apply(e) {
    this.model.diagnostics = [];
    const result = await this.model.applyActiveFilter();
    if (result.success) {
      if (result.details.diagnostics.length === 0) {
        const evt = new CustomEvent('kdsoft-done', {
          // composed allows bubbling beyond shadow root
          bubbles: true, composed: true, cancelable: true, detail: { canceled: false, model: this.model }
        });
        this.dispatchEvent(evt);
        return;
      }
      this.model.diagnostics = result.details.diagnostics;
    }
  }

  async _test(e) {
    this.model.diagnostics = [];
    const result = await this.model.testActiveFilter();
    if (result.success) {
      this.model.diagnostics = result.details.diagnostics;
    } else {
      throw (result);
    }
  }

  _save(e) {
    const evt = new CustomEvent('kdsoft-save', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model: this.model }
    });
    this.dispatchEvent(evt);
  }

  _scrollToActiveItem(filtersControl) {
    const scrollPoint = (filtersControl.clientWidth * this.model.activeFilterIndex);
    filtersControl.scrollTo({ left: scrollPoint, behavior: 'smooth' });
  }

  _filtersKeyDown(e) {
    switch (e.key) {
      case 'ArrowDown':
      case 'ArrowRight': {
        this.model.incrementActiveIndex();
        break;
      }
      case 'ArrowUp':
      case 'ArrowLeft': {
        this.model.decrementActiveIndex();
        break;
      }
      default:
        // ignore, let bubble up
        return;
    }
    e.preventDefault();
  }

  _indicatorClick(e) {
    const filterIndex = e.target.closest('li').dataset.index;
    this.model.activeFilterIndex = Number(filterIndex);
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    if (this._filterObserver) unobserve(this._filterObserver);
  }

  firstRendered() {
    // const filtersControl = this.renderRoot.getElementById('filters');
    // this._filterObserver = observe(() => this._scrollToActiveItem(filtersControl));
  }

  rendered() {
    this.schedule(() => {
      const filtersControl = this.renderRoot.getElementById('filters');
      this._scrollToActiveItem(filtersControl);
    });
  }

  static get styles() {
    return [
      css`
        #container {
          display: block;
        }

        .carousel {
          display: flex;
          flex-wrap: nowrap;
          overflow-x: hidden;
          -webkit-overflow-scrolling: touch;
          scroll-snap-type: inline mandatory;
        }

        .carousel-item {
          display: flex;
          align-items: center;
          justify-content: center;
          min-width: 100%;
          scroll-snap-align: center;
        }

        #footer {
          display: grid;
          grid-template-columns: 1fr auto 1fr;
        }

        filter-edit {
          display: block;
        }
      `,
    ];
  }

  render() {
    const activeFilterIndex = this.model.activeFilterIndex;
    const result = html`
      <link rel="stylesheet" type="text/css" href=${styleLinks.tailwind} />
      <link rel="stylesheet" type="text/css" href=${styleLinks.fontawesome} />
      <style>
        :host {
          display: block;
          width: 800px;
        }
      </style>
      <div id="container" @keydown=${this._filtersKeyDown}>
        <ul id="filters" class="carousel" tabindex="0">
        ${this.model.editFilterModels.map((filterModel, index) => {
          // model must be an object
          return html`
            <li class="carousel-item">
              <filter-edit .model=${filterModel} tabindex="0"></filter-edit>
            </li>
          `;
        })}
        </ul>
        <div id="footer" class=" mt-2">
          <div class="flex justify-start">
            <button type="button" class="py-1 px-2" @click=${this._save}><i class="fas fa-lg fa-save text-blue-500"></i></button>
            <button type="button" class="py-1 px-2" @click=${this._test}><i class="fas fa-lg fa-stethoscope text-orange-500"></i></button>
          </div>
          <ol class="text-xl text-grey-500" @click=${this._indicatorClick}>
            ${this.model.editFilterModels.map((filterModel, index) => {
              const activeClass = index === activeFilterIndex ? 'text-blue-500' : '';
              return html`<li class="inline-block w-8 cursor-pointer ${activeClass}" data-index=${index}>${index}</li>`;
            })}
          </ol>
          <div class="flex justify-end">
            <button type="button" class="py-1 px-2" @click=${this._apply}><i class="fas fa-lg fa-check text-green-500"></i></button>
            <button type="button" class="py-1 px-2" @click=${this._cancel}><i class="fas fa-lg fa-times text-red-500"></i></button>
          </div>
        </div>
      </div>
    `;
    return result;
  }
}

window.customElements.define('filter-form', FilterForm);
