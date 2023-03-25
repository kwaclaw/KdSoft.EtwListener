import { observable } from '@nx-js/observer-util';
import { LitMvvmElement, html, css } from '@kdsoft/lit-mvvm';
import { KdsDropdownModel, KdsDropdownListConnector } from '@kdsoft/lit-mvvm-components';
import checkboxStyles from '../styles/kds-checkbox-styles.js';
import fontAwesomeStyles from '../styles/fontawesome/css/all-styles.js';
import tailwindStyles from '../styles/tailwind-styles.js';
import '../components/etw-checklist.js';
import * as utils from '../js/utils.js';

class ProviderConfig extends LitMvvmElement {
  constructor() {
    super();
    this.levelDropDownModel = observable(new KdsDropdownModel());
    this.levelChecklistConnector = new KdsDropdownListConnector(
      () => this.renderRoot.getElementById('traceLevel'),
      () => this.renderRoot.getElementById('traceLevelList'),
      ProviderConfig._getSelectedText
    );
  }

  static _getSelectedText(checkListModel) {
    let result = null;
    for (const selEntry of checkListModel.selectedEntries) {
      if (result) result += `, ${selEntry.item.name}`;
      else result = selEntry.item.name;
    }
    return result;
  }

  _deleteClicked(e) {
    // send event to parent to remove from list
    const evt = new CustomEvent('delete', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model: this.model }
    });
    this.dispatchEvent(evt);
  }

  _fieldChange(e) {
    e.stopPropagation();
    this.model[e.target.name] = utils.getFieldValue(e.target);
  }

  /* eslint-disable indent, no-else-return */

  shouldRender() {
    return !!this.model;
  }

  beforeFirstRender() {
    //
  }

  static get styles() {
    return [
      tailwindStyles,
      checkboxStyles,
      fontAwesomeStyles,
      css`
        :host {
          display: block;
        }

        fieldset {
          display: contents;
        }

        label {
          font-weight: bolder;
        }

        kds-dropdown {
          width: auto;
        }

        etw-checklist {
          min-width: 200px;
          background-color: white;
          z-index: 2;
        }

        .provider {
          display: grid;
          grid-template-columns: auto auto;
          align-items: baseline;
          grid-gap: 5px;
        }

        .provider #isDisabled {
          font-size: 1rem;
          line-height: 1.5;
        }

        #keywords:invalid {
          border: 2px solid red;
        }

        input, textarea {
          border-width: 1px;
        }
      `,
    ];
  }

  render() {
    const borderColor = 'border-transparent';
    const htColor = 'text-gray-700';
    const timesClasses = 'text-gray-600 fas fa-lg fa-times';

    // Note: number inputs can be sized by setting their max value

    return html`
      <form class="bg-gray-300 p-2" @change=${this._fieldChange}>
        <div class="border-l-2 ${borderColor}">
          <header class="flex items-center justify-start pl-1 cursor-pointer select-none relative">
              <input name="name" type="text"
                class="${htColor} mr-2 w-full" 
                .value=${this.model.name}
              />
            <span class="${timesClasses} ml-auto mr-2" @click=${this._deleteClicked}></span>
          </header>
          <div class="mt-2 relative">
            <div class="provider pl-8 pb-1">
              <fieldset>
                <label class="text-gray-600" for="level">Level</label>
                <kds-dropdown id="traceLevel" class="py-0"
                  .model=${this.levelDropDownModel}
                >
                  <etw-checklist id="traceLevelList" class="text-black bg-white"
                    .model=${this.model.levelChecklistModel}
                    .itemTemplate=${item => html`${item.name}`}>
                  </etw-checklist>
                  <span slot="dropDownButtonIcon" class="fa-solid fa-lg fa-caret-down"></span>
                </kds-dropdown>
              </fieldset>
              <fieldset>
                <label class="text-gray-600" for="keywords">Match Keywords</label>
                <input id="keywords" name="matchKeywords"
                  type="number" min="0" max="99999999999999999999" .value=${this.model.matchKeywords} />
              </fieldset>
            </div>
          </div>
        </div>
      </form>
    `;
  }

  rendered() {
    // it may be necessary to reconnect the drop down connector
    this.levelChecklistConnector.reconnectDropdownSlot();
  }
}

window.customElements.define('provider-config', ProviderConfig);
