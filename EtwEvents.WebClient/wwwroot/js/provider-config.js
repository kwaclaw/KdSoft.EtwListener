import { html } from '../lib/lit-html.js';
import { LitMvvmElement, BatchScheduler } from '../lib/@kdsoft/lit-mvvm.js';
import { observe, unobserve } from '../lib/@nx-js/observer-util.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { css } from '../styles/css-tag.js';
import styleLinks from '../styles/kdsoft-style-links.js';
import * as utils from './utils.js';
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
    this.levelCheckListModel = new KdSoftCheckListModel(TraceSessionConfigModel.traceLevelList, [this.model.level || 0], false, item => item.name, item => item.value);
    this._levelObservers = this.connectLevelControls(this.levelDropDownModel, this.levelCheckListModel, 'traceLevelList', true);
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this._levelObservers.forEach(o => unobserve(o));
    this.levelCheckListModel = null;
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

  _expandClicked(e) {
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

  _deleteClicked(e) {
    // send event to parent to remove from list
    const evt = new CustomEvent('delete', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model: this.model }
    });
    this.dispatchEvent(evt);
  }

  _fieldChanged(e) {
    e.stopPropagation();
    this.model[e.target.name] = e.target.value;
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

  render() {
    const provider = this.model;
    const expanded = provider.expanded || false;
    const borderColor = expanded ? 'border-indigo-500' : 'border-transparent';
    const htColor = expanded ? 'text-indigo-700' : 'text-gray-700';
    const timesClasses = expanded ? 'text-indigo-500 fas fa-times' : 'text-gray-600 fas fa-times';
    const chevronClasses = expanded ? 'text-indigo-500 fas fa-chevron-circle-up' : 'text-gray-600 fas fa-chevron-circle-down';

    return html`
      <link rel="stylesheet" type="text/css" href=${styleLinks.tailwind} />
      <link rel="stylesheet" type="text/css" href=${styleLinks.fontawesome} />
      <link rel="stylesheet" type="text/css" href=${styleLinks.checkbox} />
      <style>
        :host {
          display: block;
        }
      </style>

      <article class="bg-gray-100 p-2" @change=${this._fieldChanged}>
      <div class="border-l-2 ${borderColor}">
        <header class="flex items-center justify-start pl-1 cursor-pointer select-none">
            <input name="name" type="text" class="${htColor} form-input mr-2 w-full" value=${provider.name} ?readonly=${!expanded} />
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
                <input id="keyWords" name="matchKeyWords" type="number" min="0" class="form-input" value=${provider.matchKeyWords} />
            </fieldset>
            <fieldset>
                <label class="text-gray-600" for="isDisabled">Disabled</label>
                <input id="isDisabled" name="disabled" type="checkbox" class="kdsoft-checkbox mt-auto mb-auto" ?checked=${provider.disabled} />
            </fieldset>
        </div>
        </div>
      </article>
    `;
  }
}

window.customElements.define('provider-config', ProviderConfig);
