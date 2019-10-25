import { html } from '../lib/lit-html.js';
import { repeat } from '../lib/lit-html/directives/repeat.js';
import { classMap } from '../lib/lit-html/directives/class-map.js';
import { observable, observe, unobserve } from '../lib/@nx-js/observer-util.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { LitMvvmElement, BatchScheduler } from '../lib/@kdsoft/lit-mvvm.js';
import { css, unsafeCSS } from '../styles/css-tag.js';
import TraceSession from './traceSession.js';
import './kdsoft-checklist.js';
import './kdsoft-dropdown.js';
import './kdsoft-tree-node.js';
import './trace-session-view.js';
import './trace-session-config.js';
import './filter-form.js';
import styleLinks from '../styles/kdsoft-style-links.js';
import myappStyleLinks from '../styles/my-app-style-links.js';
import * as utils from './utils.js';

const runBtnBase = { fas: true };
const tabBase = { 'inline-block': true, 'py-2': true, 'no-underline': true };

const classList = {
  startBtn: { ...runBtnBase, 'fa-play': true, 'text-green-500': true },
  stopBtn: { ...runBtnBase, 'fa-stop': true, 'text-red-500': true },
  tabActive: { ...tabBase, 'pl-4': true, 'pr-2': true, 'text-white': true },
  tabInactive: { ...tabBase, 'px-4': true, 'text-gray-600': true, 'hover:text-gray-200': true, 'hover:text-underline': true },
  tabButtonsActive: { 'inline-block': true },
  tabButtonsInActive: { hidden: true }
};

class MyApp extends LitMvvmElement {
  static _getSelectedText(clm) {
    let result = null;
    for (const selEntry of clm.selectedEntries) {
      if (result) result += `, ${selEntry.item.name}`;
      else result = selEntry.item.name;
    }
    return result;
  }

  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
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

  async _sessionFromProfileClicked() {
    const profile = utils.first(this.model.profileCheckListModel.selectedEntries).item;
    if (!profile) return;

    if (this.model.traceSessions.has(profile.name)) return;

    const session = new TraceSession(profile);
    await session.openSession();
    this.model.traceSessions.set(profile.name, session);
    this.model.activeSessionName = profile.name;
  }

  _formDoneHandler(e) {
    const dlg = e.currentTarget;

    dlg.close();
  }

  _editProfileClicked() {
    const profile = utils.first(this.model.profileCheckListModel.selectedEntries).item;
    if (!profile) return;

    //const profileModel = utils.mergeObjects(true, profile);  //-- this does not copy getters/setters
    const profileModel = utils.cloneObject({}, profile);
    const dlg = this.renderRoot.getElementById('dlg-config');
    //TODO pass context somehow to handlers
    // dlg.addEventListener('kdsoft-apply', this._configApplyHandler);
    // dlg.addEventListener('kdsoft-cancel', this._configCancelHandler);

    const cfg = dlg.getElementsByTagName('trace-session-config')[0];
    cfg.model = profileModel;
    dlg.showModal();
  }

  _getClickSession(e) {
    const linkElement = e.currentTarget.closest('li');
    const sessionName = linkElement.dataset.sessionName;
    return { session: this.model.traceSessions.get(sessionName), sessionName };
  }

  _sessionClicked(e) {
    const linkElement = e.currentTarget.closest('li');
    this.model.activeSessionName = linkElement.dataset.sessionName;
  }

  async _closeSessionClicked(e) {
    const { session, sessionName } = this._getClickSession(e);
    if (!session) return;

    // find they key that was inserted before (or after) the current key
    const prevKey = utils.closest(this.model.traceSessions.keys(), sessionName);

    try {
      if (session.open) {
        await session.closeRemoteSession();
      }
    } finally {
      this.model.traceSessions.delete(sessionName);
      this.model.activeSessionName = prevKey;
    }
  }

  _filterSessionClicked(e) {
    const { session, sessionName } = this._getClickSession(e);
    if (!session) return;

    const profileModel = observable(utils.cloneObject({}, session.profile));
    profileModel.applyFilter = async () => {
      await session.applyFilter(profileModel.filter);
    };

    const dlg = this.renderRoot.getElementById('dlg-filter');
    const cfg = dlg.getElementsByTagName('filter-form')[0];
    cfg.model = profileModel;
    dlg.showModal();
  }

  _eventsClicked(e) {
    const { session, sessionName } = this._getClickSession(e);
    if (!session) return;

    session.toggleEvents();
  }

  _toggleNav() {
    this.renderRoot.getElementById('nav-content').classList.toggle('hidden');
  }

  _addDialogHandlers(dlg) {
    dlg.addEventListener('kdsoft-done', this._formDoneHandler);
  }

  connectedCallback() {
    super.connectedCallback();
    this._sessionListObservers = this.connectDropdownChecklist(this.model.sessionDropdownModel, this.model.profileCheckListModel, 'sessionProfiles', true);
    this._addDialogHandlers(this.renderRoot.getElementById('dlg-filter'));
    this._addDialogHandlers(this.renderRoot.getElementById('dlg-config'));
  }

  _removeDialogHandlers(dlg) {
    dlg.removeEventListener('kdsoft-done', this._formDoneHandler);
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this._sessionListObservers.forEach(o => unobserve(o));
    this._removeDialogHandlers(this.renderRoot.getElementById('dlg-filter'));
    this._removeDialogHandlers(this.renderRoot.getElementById('dlg-config'));
  }

  firstRendered() {
    super.firstRendered();
    this.appTitle = this.getAttribute('appTitle');
  }

  static get styles() {
    return [
      css`
        :host {
          display: block;
        }
        
        kdsoft-dropdown {
          width: 300px;
        }

        kdsoft-checklist {
          min-width: 300px;
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

        .brand {
          font-family: Candara;
        }

        #tab-buttons button {
          padding-left: 0.25rem;
          padding-right: 0.25rem;
        }
      `
    ];
  }

  render() {
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

          <kdsoft-dropdown class="py-0 text-white" .model=${this.model.sessionDropdownModel}>
            <kdsoft-checklist id="sessionProfiles" class="text-black" .model=${this.model.profileCheckListModel} allow-drag-drop show-checkboxes></kdsoft-checklist>
          </kdsoft-dropdown>
          <button class="px-2 py-1" @click=${this._sessionFromProfileClicked}><i class="fas fa-lg fa-wifi text-gray-500"></i></button>
          <button class="px-2 py-1" @click=${this._editProfileClicked}><i class="fas fa-lg fa-edit text-gray-500"></i></button>

          <div class="block lg:hidden">
            <button id="nav-toggle" @click=${this._toggleNav}
                    class="flex items-center px-3 py-2 border rounded text-gray-500 border-gray-600 hover:text-white hover:border-white">
              <i class="fas fa-bars"></i>
              <!-- <svg class="fill-current h-3 w-3" viewBox="0 0 20 20" xmlns="http://www.w3.org/2000/svg"><title>Menu</title><path d="M0 3h20v2H0V3zm0 6h20v2H0V9zm0 6h20v2H0v-2z"/></svg> -->
            </button>
          </div>

          <div class="w-full flex-grow lg:flex lg:items-center lg:w-auto hidden lg:block pt-6 lg:pt-0" id="nav-content">
            <ul class="list-reset lg:flex justify-end flex-1 items-center">
            ${[...this.model.traceSessions.values()].map(ses => {
    const isActiveTab = this.model.activeSession === ses;
    const tabClasses = isActiveTab ? classList.tabActive : classList.tabInactive;
    const eventsClasses = ses.eventSession && ses.eventSession.open ? classList.stopBtn : classList.startBtn;

    return html`
                <li class="mr-2 pr-1 ${isActiveTab ? 'bg-gray-700' : ''}" data-session-name=${ses.profile.name} @click=${this._sessionClicked}>
                  <a class=${classMap(tabClasses)} href="#">${ses.profile.name}</a>
                  <div id="tab-buttons" class=${classMap(isActiveTab ? classList.tabButtonsActive : classList.tabButtonsInActive)}>
                    <button type="button" @click=${this._eventsClicked}>
                      <i class=${classMap(eventsClasses)}></i>
                    </button>
                    <button type="button" @click=${this._filterSessionClicked}>
                      <i class="fas fa-filter text-gray-500"></i>
                    </button>
                    <button type="button" @click=${this._closeSessionClicked}>
                      <i class="fas fa-lg fa-times text-gray-500"></i>
                    </button>
                  </div>
                </li>
                `;
  }
  )}
            </ul>
          </div>
        </nav>

        <!-- Main content -->
        <div class="flex-grow relative">
          <trace-session-view .model=${this.model.activeSession}></trace-session-view>
        </div>

        <dialog id="dlg-config">
          <trace-session-config></trace-session-config>
        </dialog>
        <dialog id="dlg-filter">
          <filter-form></filter-form>
        </dialog>
      </div>
    `;
  }

  rendered() {
    //
  }
}

window.customElements.define('my-app', MyApp);
