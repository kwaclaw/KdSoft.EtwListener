/* workaround for snowpack bug not processing first line imports */

import { html } from 'lit/html.js';
import { observable } from '@nx-js/observer-util/dist/es.es6.js';
import { Queue, priorities } from '@nx-js/queue-util/dist/es.es6.js';
import { LitMvvmElement, css } from '@kdsoft/lit-mvvm';
import { KdSoftDropdownModel, KdSoftDropdownChecklistConnector } from '@kdsoft/lit-mvvm-components';
import tailwindStyles from '@kdsoft/lit-mvvm-components/styles/tailwind-styles.js';
import checkboxStyles from '@kdsoft/lit-mvvm-components/styles/kdsoft-checkbox-styles.js';
import fontAwesomeStyles from '@kdsoft/lit-mvvm-components/styles/fontawesome/css/all-styles.js';
import '@kdsoft/lit-mvvm-components/kdsoft-dropdown.js';
import '@kdsoft/lit-mvvm-components/kdsoft-checklist.js';
import * as utils from '../js/utils.js';

class ProviderConfig extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
    this.levelDropDownModel = observable(new KdSoftDropdownModel());
    this.levelChecklistConnector = new KdSoftDropdownChecklistConnector(
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

  expand() {
    const oldExpanded = this.model.expanded;
    // send event to parent to collapse other providers
    if (!oldExpanded) {
      const evt = new CustomEvent('beforeExpand', {
        // composed allows bubbling beyond shadow root
        bubbles: true, composed: true, cancelable: true, detail: { model: this.model }
      });
      this.dispatchEvent(evt);
    }
    this.model.expanded = !oldExpanded;
  }

  _expandClicked(e) {
    this.expand();
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

  disconnectedCallback() {
    super.disconnectedCallback();
  }

  shouldRender() {
    return !!this.model;
  }

  firstRendered() {
    super.firstRendered();
    // DOM nodes may have changed
    this.levelChecklistConnector.reconnectDropdownSlot();
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
        fieldset {
          display: contents;
        }

        label {
          font-weight: bolder;
        }

        kdsoft-dropdown {
          width: auto;
        }

        kdsoft-checklist {
          min-width: 200px;
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
      `,
    ];
  }

  render() {
    const expanded = this.model.expanded || false;
    const borderColor = expanded ? 'border-indigo-500' : 'border-transparent';
    const htColor = expanded ? 'text-indigo-700' : 'text-gray-700';
    const timesClasses = 'text-gray-600 fas fa-lg fa-times';
    const chevronClasses = expanded
      ? 'text-indigo-500 fas fa-lg  fa-chevron-circle-up'
      : 'text-gray-600 fas fa-lg fa-chevron-circle-down';

    // Note: number inputs can be sized by setting their max value

    return html`
      <style>
        :host {
          display: block;
        }
      </style>

      <article class="bg-gray-100 p-2" @change=${this._fieldChange}>
        <div class="border-l-2 ${borderColor}">
          <header class="flex items-center justify-start pl-1 cursor-pointer select-none relative">
              <input name="name" type="text"
                class="${htColor} mr-2 w-full" 
                ?readonly=${!expanded}
                .value=${this.model.name}
              />
            <span class="${timesClasses} ml-auto mr-2" @click=${this._deleteClicked}></span>
            <span class="${chevronClasses}" @click=${this._expandClicked}></span>
          </header>
          <div class="mt-2 relative" ?hidden=${!expanded}>
            <div class="provider pl-8 pb-1">
              <fieldset>
                <label class="text-gray-600" for="level">Level</label>
                <kdsoft-dropdown id="traceLevel" class="py-0"
                  .model=${this.levelDropDownModel} .connector=${this.levelChecklistConnector}>
                  <kdsoft-checklist
                    id="traceLevelList"
                    class="text-black"
                    .model=${this.model.levelChecklistModel}
                    .getItemTemplate=${item => html`${item.name}`}>
                  </kdsoft-checklist>
                </kdsoft-dropdown>
              </fieldset>
              <fieldset>
              <label class="text-gray-600" for="keywords">Match Keywords</label>
                <input id="keywords" name="matchKeywords"
                  type="number" min="0" max="99999999999999999999" .value=${this.model.matchKeywords} />
              </fieldset>
            </div>
          </div>
        </div>
      </article>
    `;
  }
}

window.customElements.define('provider-config', ProviderConfig);
