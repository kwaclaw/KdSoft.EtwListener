import { repeat } from 'lit-html/directives/repeat.js';
import { classMap } from 'lit-html/directives/class-map.js';
import { LitMvvmElement, html, nothing, css } from '@kdsoft/lit-mvvm';
import { KdsDragDropProvider } from '@kdsoft/lit-mvvm-components';
import fontAwesomeStyles from '../styles/fontawesome/css/all-styles.js';
import tailwindStyles from '../styles/tailwind-styles.js';
import listItemCheckboxStyles from '../styles/kds-list-item-checkbox-styles.js';

const arrowBase = { 'fa-solid': true, 'fa-lg': true, 'text-gray-500': true };

const arrowClassList = {
  upArrow: { ...arrowBase, 'fa-caret-up': true, 'pt-3': true },
  downArrow: { ...arrowBase, 'fa-caret-down': true },
};

function getListItemId(item) {
  return Number(item._kdsIndex);
}

export default class EtwChecklist extends LitMvvmElement {
  constructor() {
    super();
    this.getItemTemplate = () => html``;
    this.getStyles = () => [css``.styleSheet];
  }

  get allowDragDrop() { return this.hasAttribute('allow-drag-drop'); }
  set allowDragDrop(val) {
    if (val) this.setAttribute('allow-drag-drop', '');
    else this.removeAttribute('allow-drag-drop');
  }

  get checkboxes() { return this.hasAttribute('checkboxes'); }
  set checkboxes(val) {
    if (val) this.setAttribute('checkboxes', '');
    else this.removeAttribute('checkboxes');
  }

  get arrows() { return this.hasAttribute('arrows'); }
  set arrows(val) {
    if (val) this.setAttribute('arrows', '');
    else this.removeAttribute('arrows');
  }

  // we don't derive from KdsList because we cannot render as a child, so if we want
  // to re-use the public API of KdsList then we must expose the wrapped component
  get list() { return this.renderRoot.querySelector('kds-list'); }

  // Observed attributes will trigger an attributeChangedCallback, which in turn will cause a re-render to be scheduled!
  static get observedAttributes() {
    return [...super.observedAttributes, 'allow-drag-drop', 'checkboxes', 'arrows'];
  }

  attributeChangedCallback(name, oldValue, newValue) {
    if (name === 'allow-drag-drop') {
      if (newValue === '' && !this._dragDrop) this._dragDrop = new KdsDragDropProvider(getListItemId);
      else this._dragDrop = null;
    }
    // trigger re-render
    super.attributeChangedCallback(name, oldValue, newValue);
  }

  shouldRender() {
    return !!this.model;
  }

  beforeFirstRender() {
    // if the user wants to add more styles for use by the result of getItemTemplate()
    const adopted = this.renderRoot.adoptedStyleSheets;
    this.renderRoot.adoptedStyleSheets = [...adopted, ...this.getStyles()];
  }

  /* https://philipwalton.com/articles/what-no-one-told-you-about-z-index/ */
  static get styles() {
    return [
      tailwindStyles,
      fontAwesomeStyles,
      listItemCheckboxStyles,
      css`
        kds-list::part(ul) {
          list-style: none;
          padding: 3px;
        }

        kds-list-item {
          padding: 2px;
        }

        /* if we dont have a checkbox then we indicate selection this way */
        kds-list-item[selected]:not([checkbox]) {
          background-color: darkgrey;
        }

        kds-list-item.kds-droppable {
          outline: 2px solid lightblue;
          outline-offset: -1px;
        }

        kds-list-item:hover {
          background-color: lightgrey;
        }

        kds-list-item:focus {
          outline: solid 2px rgb(50, 150, 255);
        }
      `,
    ];
  }

  // we wrap KdsList (kds-list) instead of deriving from it because we need to populate its slots
  render() {
    return html`
      <kds-list .model=${this.model}>
        ${repeat(this.model.filteredItems,
          entry => this.model.getItemId(entry.item),
          entry => html`<kds-list-item tabindex="0"
            .model=${entry.item}
            .dragDropProvider=${this._dragDrop}
            ?checkbox=${this.checkboxes}
            ?arrows=${this.arrows}
            ?up=${!entry.isFirst}
            ?down=${!entry.isLast}
            ?selected=${this.model.isItemSelected(entry.item)}
          >
            ${this.arrows
              ? html`
                <span slot="up-arrow" class=${classMap(arrowClassList.upArrow)}></span>
                <span slot="down-arrow" class=${classMap(arrowClassList.downArrow)}></span>
              `
              : nothing
            }
            <span slot="item" class="my-auto w-full">${this.getItemTemplate(entry.item)}</span>              
          </kds-list-item>`
        )}
      </kds-list>
    `;
  }
}

window.customElements.define('etw-checklist', EtwChecklist);
