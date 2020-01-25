import { html } from '../lib/lit-html.js';
import { LitMvvmElement, BatchScheduler } from '../lib/@kdsoft/lit-mvvm.js';
import { observe, unobserve } from '../lib/@nx-js/observer-util.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { css } from '../styles/css-tag.js';
import sharedStyles from '../styles/kdsoft-shared-styles.js';
import styleLinks from '../styles/kdsoft-style-links.js';
import * as utils from '../js/utils.js';
import TraceSessionConfigModel from './trace-session-config-model.js';
import KdSoftDropDownModel from './kdsoft-dropdown-model.js';
import KdSoftCheckListModel from './kdsoft-checklist-model.js';

class ProviderConfig extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
    this.levelDropDownModel = new KdSoftDropDownModel();
  }

  connectedCallback() {
    super.connectedCallback();
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this._levelObservers.forEach(o => unobserve(o));
    this.levelCheckListModel = null;
  }

  firstRendered() {
    super.firstRendered();
    this._levelObservers = this.connectLevelControls(this.levelDropDownModel, this.levelCheckListModel, 'traceLevelList', true);
  }

  static _getSelectedText(clm) {
    let result = null;
    for (const selEntry of clm.selectedEntries) {
      if (result) result += `, ${selEntry.item.name}`;
      else result = selEntry.item.name;
    }
    return result;
  }

  connectLevelControls(dropDownModel, checkListModel, checkListId, singleSelect) {
    const checkList = this.renderRoot.getElementById(checkListId);
    // react to selection changes in checklist
    const selectObserver = observe(() => {
      dropDownModel.selectedText = ProviderConfig._getSelectedText(checkListModel);
      // single select: always close up on selecting an item
      if (singleSelect) checkList.blur();
      const selEntry = utils.first(checkListModel.selectedEntries);
      if (selEntry) { this.model.level = selEntry.item.value; }
    });

    // react to search text changes in dropdown
    const searchObserver = observe(() => {
      const regex = new RegExp(dropDownModel.searchText, 'i');
      checkListModel.filter = item => regex.test(item.name);
    });

    const droppedObserver = observe(() => {
      if (dropDownModel.dropped) {
        // queue this at the end of updates to be rendered correctly
        checkList.scheduler.add(() => checkList.initView());
      }
    });

    return [selectObserver, searchObserver, droppedObserver];
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
    let val;
    switch (e.target.type) {
      case 'number':
        val = e.target.valueAsNumber;
        break;
      case 'date':
        val = e.target.valueAsDate;
        break;
      case 'checkbox':
        val = e.target.checked;
        break;
      default:
        val = e.target.value;
        break;
    }
    this.model[e.target.name] = val;
  }

  static get styles() {
    return [
      css`
        fieldset {
          display: contents;
        }

        label {
          font-weight: bolder;
        }

        kdsoft-dropdown {
          width: 200px;
        }

        kdsoft-checklist {
          min-width: 200px;
        }

        .provider {
          display: grid;
          grid-template-columns: 1fr 2fr;
          align-items: baseline;
          grid-gap: 5px;
        }

        .provider #isDisabled {
          font-size: 1rem;
          line-height: 1.5;
        }

        #keyWords:invalid {
          border: 2px solid red;
        }
      `,
    ];
  }

  /* eslint-disable indent, no-else-return */

  render() {
    if (!this._firstRendered) {
      // first time we know that this.model is defined for certain
      this.levelCheckListModel = new KdSoftCheckListModel(
        TraceSessionConfigModel.traceLevelList,
        [this.model.level || 0],
        false,
        item => html`${item.name}`,
        item => item.value
      );
    }

    const expanded = this.model.expanded || false;
    const borderColor = expanded ? 'border-indigo-500' : 'border-transparent';
    const htColor = expanded ? 'text-indigo-700' : 'text-gray-700';
    const timesClasses = 'text-gray-600 fas fa-lg fa-times';
    const chevronClasses = expanded ? 'text-indigo-500 fas fa-lg  fa-chevron-circle-up' : 'text-gray-600 fas fa-lg fa-chevron-circle-down';

    return html`
      ${sharedStyles}
      <link rel="stylesheet" type="text/css" href=${styleLinks.checkbox} />
      <style>
        :host {
          display: block;
        }
      </style>

      <article class="bg-gray-100 p-2" @change=${this._fieldChange}>
        <div class="border-l-2 ${borderColor}">
          <header class="flex items-center justify-start pl-1 cursor-pointer select-none">
            <input name="name" type="text"
              class="${htColor} form-input mr-2 w-full" 
              ?readonly=${!expanded}
              .value=${this.model.name}
            />
            <span class="${timesClasses} w-7 h-7 ml-auto mr-2" @click=${this._deleteClicked}></span>
            <span class="${chevronClasses} w-7 h-7" @click=${this._expandClicked}></span>
          </header>
          <div class="mt-2" ?hidden=${!expanded}>
            <div class="provider pl-8 pb-1">
              <fieldset>
                <label class="text-gray-600" for="level">Level</label>
                <kdsoft-dropdown id="traceLevel" class="py-0" .model=${this.levelDropDownModel}>
                  <kdsoft-checklist id="traceLevelList" class="text-black" .model=${this.levelCheckListModel}></kdsoft-checklist>
                </kdsoft-dropdown>
              </fieldset>
              <fieldset>
                <label class="text-gray-600" for="keyWords">MatchKeyWords</label>
                <input id="keyWords" name="matchKeyWords" type="number" min="0" class="form-input" .value=${this.model.matchKeyWords} />
              </fieldset>
              <fieldset>
                <label class="text-gray-600" for="isDisabled">Disabled</label>
                <input id="isDisabled" name="disabled" type="checkbox" class="kdsoft-checkbox mt-auto mb-auto" .checked=${!!this.model.disabled} />
              </fieldset>
            </div>
          </div>
        </div>
      </article>
    `;
  }
}

window.customElements.define('provider-config', ProviderConfig);