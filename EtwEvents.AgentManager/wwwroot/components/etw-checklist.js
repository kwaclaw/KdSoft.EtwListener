import { classMap } from 'lit-html/directives/class-map.js';
import { html, nothing, css } from '@kdsoft/lit-mvvm';
import { KdsList, KdsDragDropProvider } from '@kdsoft/lit-mvvm-components';
import fontAwesomeStyles from '../styles/fontawesome/css/all-styles.js';
import tailwindStyles from '../styles/tailwind-styles.js';
import listItemCheckboxStyles from '../styles/kds-list-item-checkbox-styles.js';

const arrowBase = { 'fa-solid': true, 'fa-lg': true, 'text-gray-500': true };

const arrowClassList = {
  upArrow: { ...arrowBase, 'fa-caret-up': true, 'pt-3': true },
  downArrow: { ...arrowBase, 'fa-caret-down': true },
};

function idToIndex(id) {
  return Number(id.substring('kds-item-'.length));
}

function onItemDrop(e) {
  const fromIndex = idToIndex(e.detail.fromId);
  const toIndex = idToIndex(e.detail.toId);
  this.model.moveItem(fromIndex, toIndex);

  this.schedule(() => {
    const dropped = this.renderRoot.getElementById(e.detail.toId);
    if (dropped) dropped.focus();
  });
}

function getListItemId(element) {
  return element.id;
}

export default class EtwChecklist extends KdsList {
  constructor() {
    super();
    this.itemTemplate = () => nothing;
    this.itemStyleSheets = () => [];
    // use fixed reference to be able to add *and* remove as event listener
    this._onItemDrop = onItemDrop.bind(this);
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

  connectedCallback() {
    super.connectedCallback();
    this.addEventListener('kds-drop', this._onItemDrop);
  }

  disconnectedCallback() {
    this.removeEventListener('kds-drop', this._onItemDrop);
    super.disconnectedCallback();
  }

  // override to return the DOM element for the item's index
  getItemElementByIndex(index) {
    return this.renderRoot.getElementById(`kds-item-${index}`);
  }

  // override to return the item's index from the item's DOM element
  getItemIndexFromElement(element) {
    return idToIndex(element.id);
  }

  /* https://philipwalton.com/articles/what-no-one-told-you-about-z-index/ */
  static get styles() {
    return [
      ...super.styles,
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

  beforeFirstRender() {
    super.beforeFirstRender();
    this.renderRoot.adoptedStyleSheets = [...this.renderRoot.adoptedStyleSheets, ...this.itemStyleSheets()];
  }

  renderItem(entry) {
    return html`
      <kds-list-item tabindex="0"
        .model=${entry.item}
        .dragDropProvider=${this._dragDrop}
        ?checkbox=${this.checkboxes}
        ?arrows=${this.arrows}
        ?up=${!entry.isFirst}
        ?down=${!entry.isLast}
        ?selected=${this.model.isItemSelected(entry.item)}
        id="kds-item-${entry.index}"
      >
        ${this.arrows
          ? html`
            <span slot="up-arrow" class=${classMap(arrowClassList.upArrow)}></span>
            <span slot="down-arrow" class=${classMap(arrowClassList.downArrow)}></span>
          `
          : nothing
        }
        <span slot="item" class="my-auto w-full inline-flex">${this.itemTemplate(entry.item)}</span>              
      </kds-list-item>
    `;
  }
}

window.customElements.define('etw-checklist', EtwChecklist);
