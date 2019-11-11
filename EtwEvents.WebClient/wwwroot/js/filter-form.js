﻿import { html } from '../lib/lit-html.js';
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

  async _apply(e) {
    const filterModel = this.model.activeFilterModel;
    filterModel.diagnostics = [];
    const result = await this.model.session.applyFilter(filterModel.filter);
    if (result.success) {
      if (result.details.diagnostics.length === 0) {
        this.model.postFormData();
        const evt = new CustomEvent('kdsoft-done', {
          // composed allows bubbling beyond shadow root
          bubbles: true, composed: true, cancelable: true, detail: { canceled: false, model: this.model }
        });
        this.dispatchEvent(evt);
        return;
      }
      filterModel.diagnostics = result.details.diagnostics;
    }
  }

  async _test(e) {
    const filterModel = this.model.activeFilterModel;
    filterModel.diagnostics = [];
    const result = await this.model.session.testFilter(filterModel.filter);
    if (result.success) {
      filterModel.diagnostics = result.details.diagnostics;
    } else {
      throw (result);
    }
  }

  _save(e) {
    this.model.postFormData();
    const evt = new CustomEvent('kdsoft-save', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model: this.model }
    });
    this.dispatchEvent(evt);
  }

  _add(e) {
    this.model.addFilterModel();
  }

  _remove(e) {
    e.stopPropagation();
    this.model.removeActiveFilter();
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
        }
      </style>
      <div id="container" @keydown=${this._filtersKeyDown}>
        <ul id="filters" class="carousel" tabindex="0">
        ${this.model.filterModels.map((filterModel, index) => {
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
          <div>
            <ol class="inline-block text-xl text-gray-500" @click=${this._indicatorClick}>
              ${this.model.filterModels.map((filterModel, index) => {
                if (index === activeFilterIndex) {
                  return html`<li class="inline-flex justify-around items-center w-10 px-1 mx-1 cursor-pointer text-blue-500 bg-blue-100 border border-gray-100 rounded-full" data-index=${index}>
                    ${index + 1}
                    <button type="button" class="mr-1 text-gray-600" @click=${this._remove}><i class="fas fa-times"></i></button>
                  </li>`;
                } else {
                  return html`<li class="inline-flex justify-center items-center w-8 cursor-pointer" data-index=${index}>${index + 1}</li>`;
                }
              })}
            </ol>
            <button type="button" class="px-1" @click=${this._add}><i class="fas fa-lg fa-plus text-gray-500"></i></button>
          </div>
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
