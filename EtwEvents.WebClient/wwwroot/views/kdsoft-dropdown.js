import { html } from '../lib/lit-html.js';
import { LitMvvmElement, css } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import sharedStyles from '../styles/kdsoft-shared-styles.js';

function isChildOf(parent, child) {
  if (!child) {
    return false;
  }

  while (child.parentNode !== parent) {
    child = child.parentNode;
    if (child == null) {
      return false;
    }
  }
  return true;
}

class KdSoftDropdown extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
  }

  get connector() { return this._connector; }
  set connector(value) { this._connector = value; }

  _hostLostFocus(e) {
    if (e.currentTarget === e.relatedTarget || isChildOf(e.currentTarget, e.relatedTarget)) {
      return;
    }
    this.model.dropped = false;
  }

  _hostFocused(e) {
    this.model.dropped = true;
  }

  _seltextFocused(e) {
    e.preventDefault();
    const seltext = e.currentTarget;
    const searchbox = e.currentTarget.nextElementSibling;

    // because of this, seltext will not receive the click and mouseup events
    seltext.setAttribute('hidden', '');
    searchbox.removeAttribute('hidden');
    searchbox.focus();
  }

  _seltextLostFocus(e) {
    e.preventDefault();
  }

  _searchboxFocused(e) {
    e.preventDefault();
    this.model.dropped = true;
  }

  _searchboxLostFocus(e) {
    e.preventDefault();
    const seltext = e.currentTarget.previousElementSibling;
    seltext.removeAttribute('hidden');
    e.currentTarget.setAttribute('hidden', '');
  }

  _searchTextChanged(e) {
    this.model.searchText = e.currentTarget.value;
  }

  _dropdownButtonFocused(e) {
    // need to exclude this button from the host's focus handler, because its click handler
    // performs the drop-down toggle function which would be in conflict
    e.preventDefault();
    e.stopImmediatePropagation();
  }

  _dropdownButtonClicked(e) {
    this.model.dropped = !this.model.dropped;
  }

  _slotKeydown(e) {
    if (e.key === 'Enter') {
      e.preventDefault();
      this.model.dropped = false;
      const btn = this.shadowRoot.getElementById('dropDownButton');
      btn.focus();
    }
  }

  /* eslint-disable indent, no-else-return */

  disconnectedCallback() {
    super.disconnectedCallback();
    if (this.connector) this.connector.disconnectDropdownSlot();
  }

  beforeFirstRender() {
    super.beforeFirstRender();
    if (this.connector) this.connector.connectDropdownSlot();
    this.shadowRoot.host.addEventListener('focusout', this._hostLostFocus);
    this.shadowRoot.host.addEventListener('focusin', this._hostFocused);
  }

  static get styles() {
    return [
      css`
        #container {
          display: flex;
          align-items: baseline;
          justify-items: flex-end;
        }
        #dropdown {
          position: relative;
          /* height: 0; */
        }
        #seltext {
          pointer-events: auto;
        }
        slot {
          display: flex;
          position: absolute;
          left: 0;
          top: 0;
        }
      `,
    ];
  }

  render() {
    const selText = this.model.selectedText;
    const result = html`
      ${sharedStyles}
      <style>
        :host {
          display: inline-block;
          line-height: 1.25em;
        }
      </style>
      <div id="container" class="border">
        <span id="seltext"
          class="my-auto p-1 flex-grow whitespace-no-wrap overflow-x-hidden"
          tabindex="1"
          title="${selText}"
          @focus="${this._seltextFocused}"
          @blur="${this._seltextLostFocus}"
        >
          ${selText}
        </span>
        <input id="searchbox"
          type="text"
          tabindex="1"
          class="my-auto py-1 flex-grow"
          @focus="${this._searchboxFocused}"
          @blur="${this._searchboxLostFocus}"
          @input="${this._searchTextChanged}"
          hidden />
        <button id="dropDownButton"
          type="button"
          tabindex="3"
          class="py-1 my-auto px-2 bg-gray-300 hover:bg-gray-400"
          @focusin="${this._dropdownButtonFocused}"
          @click="${this._dropdownButtonClicked}"
        >
          <span id="dropDownButtonIcon" class="fas fa-caret-down"></span>
        </button>
      </div>
      <div id="dropdown" class="z-50" ?hidden=${!this.model.dropped}>
        <slot tabindex="2" @keydown=${this._slotKeydown}>No dropdown content provided.</slot>
      </div>
    `;
    return result;
  }
}

window.customElements.define('kdsoft-dropdown', KdSoftDropdown);
