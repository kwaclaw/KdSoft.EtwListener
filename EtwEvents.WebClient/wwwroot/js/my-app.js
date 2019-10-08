import { html } from '../lib/lit-html.js';
import { repeat } from '../lib/lit-html/directives/repeat.js';
import { observable, observe, unobserve } from '../lib/@nx-js/observer-util.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { LitMvvmElement } from '../lib/@kdsoft/lit-mvvm.js';
import { css, unsafeCSS } from '../styles/css-tag.js';
import './my-grid.js';
import MyAppModel from './myAppModel.js';
import TraceSession from './traceSession.js';
import EventSession from './eventSession.js';
import './kdsoft-checklist.js';
import './kdsoft-dropdown.js';
import './kdsoft-tree-node.js';
import KdSoftCheckListModel from './kdsoft-checklist-model.js';
import KdSoftDropdownModel from './kdsoft-dropdown-model.js';
import styleLinks from '../styles/kdsoft-style-links.js';

function* makeEmptyIterator() {
  //
}

class MyApp extends LitMvvmElement {
  static get styles() {
    return [
      css`
        :host {
          display: block;
        }
        
        .main-content {
          height: calc(100vh - 34px);
          position: relative;
        }

        kdsoft-dropdown {
          width: 300px;
        }

        #droplistSingle, #droplistMulti {
          --max-scroll-height: 200px;
        }

        #txt {
          width: 100%;
          max-height: 100%;
          min-height: 400px;
          color: black:
        }
      `
    ];
  }

  static _getSelectedText(clm) {
    let result = null;
    for (const selEntry of clm.selectedEntries) {
      if (result) result += `, ${selEntry.item.text}`;
      else result = selEntry.item.text;
    }
    return result;
  }

  constructor() {
    super();

    const providerItems = [
      {
        id: 1,
        disabled: false,
        get text() { return this.name; },
        name: 'Microsoft-Windows-Application Server-Applications',
        level: 3,
        matchKeyWords: 2305843009213825068
      },
      {
        id: 2,
        disabled: false,
        get text() { return this.name; },
        name: 'SmartClinic-Services-Mobility',
        level: 4,
        matchKeyWords: 0
      },
      {
        id: 3,
        disabled: false,
        get text() { return this.name; },
        name: 'SmartClinic-Services-Interop',
        level: 4,
        matchKeyWords: 0
      },
      {
        id: 4,
        disabled: false,
        get text() { return this.name; },
        name: 'Microsoft-Windows-DotNETRuntime',
        level: 4,
        matchKeyWords: 0
      },
    ];

    this.multiChecklistModel = new KdSoftCheckListModel(providerItems, [0, 1, 3], true);
    this.multiDropdownModel = new KdSoftDropdownModel();
  }

  connectDropdownChecklist(dropDownModel, checkListModel, checkListId, singleSelect) {
    const checkList = this.shadowRoot.getElementById(checkListId);
    // react to selection changes in checklist
    const selectObserver = observe(() => {
      dropDownModel.selectedText = MyApp._getSelectedText(checkListModel);
      // single select: always close up on selecting an item
      if (singleSelect) checkList.blur();
    });

    // react to search text changes in dropdown
    const searchObserver = observe(() => {
      const regex = new RegExp(dropDownModel.searchText, 'i');
      checkListModel.filter = item => regex.test(item.text);
    });

    const droppedObserver = observe(() => {
      if (dropDownModel.dropped) {
        // queue this at the end of updates to be rendered correctly
        checkList.scheduler.add(() => checkList.initView());
      }
    });

    return [selectObserver, searchObserver, droppedObserver];
  }

  async _closeRemoteSession(name) {
    try {
      const response = await fetch(`/Etw/CloseRemoteSession?name=${name}`, {
        method: 'POST', // or 'PUT'
        headers: {
          'Content-Type': 'application/json'
        }
      });

      const jobj = await response.json();
      if (response.ok) {
        this.model.traceSession = null;
        console.log('Success:', JSON.stringify(jobj));
      }
      else {
        this.model.traceSession = null;
        console.log('Error:', JSON.stringify(jobj));
      }
    } catch (error) {
      console.error('Error:', error);
    }
  }

  async _openSession(name, host, providers) {
    const request = { name, host, providers, lifeTime: 'PT6M30S' };

    try {
      const response = await fetch('/Etw/OpenSession', {
        method: 'POST', // or 'PUT'
        body: JSON.stringify(request), // data can be `string` or {object}!
        headers: {
          'Content-Type': 'application/json'
        }
      });

      const jobj = await response.json();
      if (response.ok) {
        this.model.traceSession = new TraceSession(request.name, jobj.enabledProviders, jobj.failedProviders);
        console.log('Success:', JSON.stringify(jobj));
      }
      else {
        this.model.traceSession = null;
        console.log('Error:', JSON.stringify(jobj));
      }
    } catch (error) {
      this.model.traceSession = null;
      console.error('Error:', error);
    }
  }

  async _sessionClicked() {
    if (this.model.traceSession) {
      await this._closeRemoteSession(this.model.traceSession.name);
    } else {
      const providers = [];
      for (const entry of this.multiChecklistModel.selectedEntries) {
        providers.push(entry.item);
      }
      await this._openSession('TestSession', 'localhost:50051', providers);
    }
  }

  _eventsClicked() {
    const ts = this.model.traceSession;
    if (!ts) return;
    if (!ts.eventSession) {
      ts.eventSession = new EventSession(`ws://${window.location.host}/Etw/StartEvents?sessionName=${ts.name}`, 2000);
    }
    if (!ts.eventSession.ws) {
      ts.eventSession.connect();
    } else {
      ts.eventSession.disconnect();
    }
  }

  connectedCallback() {
    super.connectedCallback();
    this._multiSelectObservers = this.connectDropdownChecklist(this.multiDropdownModel, this.multiChecklistModel, 'droplistMulti');
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this._multiSelectObservers.forEach(o => unobserve(o));
  }

  firstRendered() {
    super.firstRendered();
    this.appTitle = this.getAttribute('appTitle');
  }

  render() {
    const ts = this.model.traceSession;
    const sessionLabel = ts ? `Close ${ts.name}` : 'Open Session';
    const eventsLabel = ts && ts.eventSession && ts.eventSession.ws ? 'Stop Events' : 'Start Events';
    const itemIterator = (ts && ts.eventSession) ? ts.eventSession.itemIterator() : makeEmptyIterator();

    return html`
      <link rel="stylesheet" type="text/css" href=${styleLinks.tailwind} />
      <link rel="stylesheet" type="text/css" href=${styleLinks.fontAwesome} />

      <!-- Header -->
      <div>
        <button @click=${this._sessionClicked}>${sessionLabel}</button>
        <kdsoft-dropdown .model=${this.multiDropdownModel}>
          <kdsoft-checklist id="droplistMulti" .model=${this.multiChecklistModel} allow-drag-drop show-checkboxes></kdsoft-checklist>
        </kdsoft-dropdown>
        <button @click=${this._eventsClicked} ?disabled=${!ts}>${eventsLabel}</button>
      </div>

      <!-- Main content -->
      <div class="main-content">
        <textarea id="txt">
          ${repeat(
            itemIterator,
            item => item.timeStampSecs + (item.timeStampNanos / 1e9),
            (item, indx) => html`${item.taskName}-${item.opCode} === ${item.providerName} === ${item.id}-${item.level}\n`
          )}
        </textarea>
      </div>
    `;
  }


  rendered() {
    //
  }
}

window.customElements.define('my-app', MyApp);
