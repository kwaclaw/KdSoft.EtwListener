import { html } from '../lib/lit-html.js';
import { repeat } from '../lib/lit-html/directives/repeat.js';
import { observable, observe, unobserve } from '../lib/@nx-js/observer-util.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { LitMvvmElement, BatchScheduler } from '../lib/@kdsoft/lit-mvvm.js';
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
import myappStyleLinks from '../styles/my-app-style-links.js';
import { SyncFusionGridStyle } from '../styles/css-grid-syncfusion-style.js';

function* makeEmptyIterator() {
  //
}

class MyApp extends LitMvvmElement {
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
    this.scheduler = new BatchScheduler(100);

    //this._dtFormat = new Intl.DateTimeFormat('default', { dateStyle: 'short', timeStyle: 'short' });
    this._dtFormat = new Intl.DateTimeFormat('default', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: 'numeric',
      minute: 'numeric',
      second: 'numeric',
      milli: 'numeric'
    });

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
      // scroll bug in Chrome - will not show more than about 1000 items, works fine with FireFox
      ts.eventSession = new EventSession(`ws://${window.location.host}/Etw/StartEvents?sessionName=${ts.name}`, 900);
    }
    if (!ts.eventSession.ws) {
      ts.eventSession.connect();
    } else {
      ts.eventSession.disconnect();
    }
  }

  _toggleNav() {
    this.renderRoot.getElementById('nav-content').classList.toggle('hidden');
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

  static get styles() {
    return [
      SyncFusionGridStyle,
      css`
        :host {
          display: block;
        }
        
        kdsoft-dropdown {
          width: 300px;
        }

        #droplistSingle, #droplistMulti {
          --max-scroll-height: 200px;
        }

        #main {
          height: 100%;
          position: relative;
          display: flex;
          flex-direction: column;
          flex-wrap: nowrap;
          justify-content: flex-start;
          align-items: stretch;
        }

        #grid {
          grid-template-columns: 1fr 1fr 1fr 1fr 1fr;
          position: absolute;
          top: 0;
          bottom: 0;
          left: 0;
          right: 0;
          overflow-x: auto;
          overflow-y: auto;
          -webkit-overflow-scrolling: touch;
          pointer-events: auto;
          z-index: 20;
        }

        .brand {
          font-family: Candara;
        }
      `
    ];
  }

  render() {
    const ts = this.model.traceSession;
    const sessionLabel = ts ? `Close ${ts.name}` : 'Open Session';
    const eventsLabel = ts && ts.eventSession && ts.eventSession.ws ? 'Stop Events' : 'Start Events';
    const itemIterator = (ts && ts.eventSession) ? ts.eventSession.itemIterator() : makeEmptyIterator();
    this.counted = 0;

    return html`
      <link rel="stylesheet" type="text/css" href=${styleLinks.tailwind} />
      <link rel="stylesheet" type="text/css" href=${styleLinks.fontawesome} />
      <link rel="stylesheet" type="text/css" href=${myappStyleLinks.myapp} />
      <style>
        :host {

        }
      </style>

      <div id="main">
        <nav class="flex-grow-0 flex items-center justify-between flex-wrap bg-gray-800 py-2 w-full z-30">
          <div class="flex items-center flex-shrink-0 text-white mr-6">
            <a class="text-white no-underline hover:text-white hover:no-underline" href="#">
              <span class="text-2xl pl-2 brand"><i class="brand"></i>KDS</span>
            </a>
          </div>

          <button class="btn btn-gray" @click=${this._sessionClicked}>${sessionLabel}</button>
          <kdsoft-dropdown class="py-0" .model=${this.multiDropdownModel}>
            <kdsoft-checklist id="droplistMulti" .model=${this.multiChecklistModel} allow-drag-drop show-checkboxes></kdsoft-checklist>
          </kdsoft-dropdown>
          <button class="btn btn-gray" @click=${this._eventsClicked} ?disabled=${!ts}>${eventsLabel}</button>

          <div class="block lg:hidden">
            <button id="nav-toggle" @click=${this._toggleNav}
                    class="flex items-center px-3 py-2 border rounded text-gray-500 border-gray-600 hover:text-white hover:border-white">
              <i class="fas fa-bars"></i>
              <!-- <svg class="fill-current h-3 w-3" viewBox="0 0 20 20" xmlns="http://www.w3.org/2000/svg"><title>Menu</title><path d="M0 3h20v2H0V3zm0 6h20v2H0V9zm0 6h20v2H0v-2z"/></svg> -->
            </button>
          </div>

          <div class="w-full flex-grow lg:flex lg:items-center lg:w-auto hidden lg:block pt-6 lg:pt-0" id="nav-content">
            <ul class="list-reset lg:flex justify-end flex-1 items-center">
              <li class="mr-3">
                <a class="inline-block py-2 px-4 text-white no-underline" href="#">Active</a>
              </li>
              <li class="mr-3">
                <a class="inline-block text-gray-600 no-underline hover:text-gray-200 hover:text-underline py-2 px-4" href="#">link</a>
              </li>
              <li class="mr-3">
                <a class="inline-block text-gray-600 no-underline hover:text-gray-200 hover:text-underline py-2 px-4" href="#">link</a>
              </li>
              <li class="mr-3">
                <a class="inline-block text-gray-600 no-underline hover:text-gray-200 hover:text-underline py-2 px-4" href="#">link</a>
              </li>
            </ul>
          </div>
        </nav>

        <!-- Main content -->
        <div class="flex-grow relative">
          <div id="grid" class="sfg-container">
            <div class="sfg-header-row">
              <div class="sfg-header">Sequence No</div>
              <div class="sfg-header">Task</div>
              <div class="sfg-header">OpCode</div>
              <div class="sfg-header">TimeStamp</div>
              <div class="sfg-header">Level</div>
            </div>
            ${repeat(
              itemIterator,
              item => item.sequenceNo,
              (item, indx) => {
                //const dateString = this._dtFormat.format(new Date(item.timeStamp));
                const dateString = `${this._dtFormat.format(item.timeStamp)}.${item.timeStamp % 1000}`;
                this.counted += 1;
                return html`
              <div class="sfg-row">
                <div>${item.sequenceNo}</div><div>${item.taskName}</div><div>${item.opCode}</div><div>${dateString}</div><div>${item.level}</div>
              </div>`;
              }
            )}
          </div>
        </div>
      </div>
    `;
  }
  
  rendered() {
    console.log(`Repeat count: ${this.counted}`);

    const grid = this.renderRoot.getElementById('grid');
    grid.scrollTop = grid.scrollHeight;
  }
}

window.customElements.define('my-app', MyApp);
