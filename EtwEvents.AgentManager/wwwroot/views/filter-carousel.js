import { html } from '../lib/lit-html.js';
import { LitMvvmElement, css } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util/dist/es.es6.js';
import sharedStyles from '../styles/kdsoft-shared-styles.js';
import './filter-edit.js';

class FilterCarousel extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
    //this.scheduler = new BatchScheduler(300);
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
    filtersControl.scroll({ left: scrollPoint, behavior: 'smooth' });
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

  /* eslint-disable indent, no-else-return */

  shouldRender() {
    return !!this.model;
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
          position: relative;
          height: 100%;
          display: flex;
          flex-direction: column;
          align-items: stretch;
        }

        .carousel {
          flex: 1 1 auto;
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
          scroll-snap-align: center center;
        }

        #footer {
          margin-top: auto;
          display: grid;
          grid-template-columns: 1fr auto 1fr;
        }

        filter-edit {
          display: block;
          height: 100%;
          overflow-y: auto;
        }
      `,
    ];
  }

  render() {
    const activeFilterIndex = this.model.activeFilterIndex;
    const result = html`
      ${sharedStyles}
      <style>
        :host {
          display: block;
        }
      </style>
      <div id="container" @keydown=${this._filtersKeyDown}>
        <ul id="filters" class="carousel" tabindex="0">
          ${this.model.filterModels.map((filterModel, index) => html`
              <li class="carousel-item" tabindex="0">
                <filter-edit .model=${filterModel}></filter-edit>
              </li>
            `
          )}
        </ul>
        <hr class="my-4" />
        <div id="footer">
          <div class="flex justify-start">
            <slot name="start"></slot>
          </div>
          <div>
            <ol class="inline-block text-xl text-gray-500" @click=${this._indicatorClick}>
              ${this.model.filterModels.map((filterModel, index) => {
                if (index === activeFilterIndex) {
                  return html`<li class="inline-flex justify-around items-center px-1 mx-1 cursor-pointer text-blue-500 bg-blue-100 border border-gray-100 rounded-full" data-index=${index}>
                    ${index + 1}
                    <button type="button" class="mx-1 text-gray-600" @click=${this._remove}><i class="fas fa-times"></i></button>
                  </li>`;
                } else {
                  return html`<li class="inline-flex justify-center items-center px-1 mx-1 cursor-pointer" data-index=${index}>${index + 1}</li>`;
                }
              })}
            </ol>
            <button type="button" class="px-1" @click=${this._add}><i class="fas fa-lg fa-plus text-gray-500"></i></button>
          </div>
          <div class="flex justify-end">
            <slot name="end"></slot>
          </div>
        </div>
      </div>
    `;
    return result;
  }
}

window.customElements.define('filter-carousel', FilterCarousel);
