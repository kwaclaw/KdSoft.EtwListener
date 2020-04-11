/* global i18n */

import { html, nothing } from '../lib/lit-html.js';
import { repeat } from '../lib/lit-html/directives/repeat.js';
import { classMap } from '../lib/lit-html/directives/class-map.js';
import { observe, unobserve } from '../lib/@nx-js/observer-util.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { LitMvvmElement, BatchScheduler } from '../lib/@kdsoft/lit-mvvm.js';
import { css, unsafeCSS } from '../styles/css-tag.js';
import FilterFormModel from './filter-form-model.js';
import './kdsoft-checklist.js';
import './kdsoft-dropdown.js';
import './kdsoft-tree-node.js';
import './trace-session-view.js';
import './trace-session-config.js';
import './filter-form.js';
import TraceSessionConfigModel from './trace-session-config-model.js';
import sharedStyles from '../styles/kdsoft-shared-styles.js';
import { KdSoftGridStyle } from '../styles/kdsoft-grid-style.js';
import myappStyleLinks from '../styles/my-app-style-links.js';
import Spinner from '../js/spinner.js';

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

function formDoneHandler(e) {
  if (!e.detail.canceled) {
    if (e.target.localName === 'filter-form') {
      this.model.saveProfile(e.detail.model.session.profile);
    } else if (e.target.localName === 'trace-session-config') {
      this.model.saveProfile(e.detail.model.cloneAsProfile());
    }
  }

  const dlg = e.currentTarget;
  dlg.close();
}

function formSaveHandler(e) {
  if (e.target.localName === 'filter-form') {
    this.model.saveProfile(e.detail.model.session.profile);
  } else if (e.target.localName === 'trace-session-config') {
    this.model.saveProfile(e.detail.model.cloneAsProfile());
  }
}


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
    // setting model property here because we cannot reliable set it from a non-lit-html rendered HTML page
    this.scheduler = new Queue(priorities.HIGH);
    // we must assign the model *after* the scheduler, or assign it externally
    // this.model = new MyAppModel(); --

    // this allows us to unregister the event handlers, because we maintain references to their instances
    this._formDoneHandler = formDoneHandler.bind(this);
    this._formSaveHandler = formSaveHandler.bind(this);

    window.myapp = this;
  }

  async _openSessionFromProfileClick(e, profile) {
    const spinner = new Spinner(e.currentTarget);
    await this.model.openSessionFromProfile(profile, spinner);
  }

  _editSessionProfileClick(e, profile) {
    const configModel = new TraceSessionConfigModel(profile);

    const dlg = this.renderRoot.getElementById('dlg-config');
    const cfg = dlg.getElementsByTagName('trace-session-config')[0];
    cfg.model = configModel;
    dlg.showModal();
  }

  _importProfilesClick(e) {
    const fileDlg = this.renderRoot.getElementById('import-profiles');
    fileDlg.click();
  }

  _importProfilesSelected(e) {
    this.model.importProfiles(e.currentTarget.files);
  }

  _deleteProfileClick(e, profileName) {
    e.stopPropagation();
    this.model.deleteProfile(profileName.toLowerCase());
  }


  _sessionTabClick(e) {
    const linkElement = e.currentTarget.closest('li');
    this.model.activeSessionName = linkElement.dataset.sessionName;
  }

  async _closeSessionClick(e, session) {
    if (!session) return;

    const spinner = new Spinner(e.currentTarget);
    await this.model.closeSession(session, spinner);
  }

  _showSessionClick(e, session) {
    if (!session) return;
    this.model.showSession(session.name);
  }

  _hideSessionClick(e, session) {
    if (!session) return;
    this.model.hideSession(session.name);
  }

  _filterSessionClick(e, session) {
    if (!session) return;

    const dlg = this.renderRoot.getElementById('dlg-filter');
    const cfg = dlg.getElementsByTagName('filter-form')[0];
    cfg.model = new FilterFormModel(session);
    dlg.showModal();
  }

  _toggleSessionEvents(e, session) {
    if (!session) return;

    const spinner = new Spinner(e.currentTarget);
    session.toggleEvents(spinner);
  }


  _toggleNav() {
    this.renderRoot.getElementById('nav-content').classList.toggle('hidden');
  }

  _addDialogHandlers(dlg) {
    dlg.addEventListener('kdsoft-done', this._formDoneHandler);
    dlg.addEventListener('kdsoft-save', this._formSaveHandler);
  }

  _removeDialogHandlers(dlg) {
    dlg.removeEventListener('kdsoft-done', this._formDoneHandler);
    dlg.removeEventListener('kdsoft-save', this._formSaveHandler);
  }

  _showErrors() {
    this.model.showErrors = true;
  }

  _errSizeDown(e) {
    if (e.buttons !== 1) return;

    this._resizeEl = this.renderRoot.getElementById('error-resizable');
    this.model.keepErrorsOpen();

    e.currentTarget.setPointerCapture(e.pointerId);
    e.currentTarget.onpointermove = ev => this._errSizeChange(ev);
  }

  _errSizeChange(e) {
    if (!this._resizeEl) return;

    const h = this._resizeEl.offsetHeight;
    const dy = e.offsetY;
    if (e.y === 0 || dy === 0) return;

    const newHeightStyle = `${h - dy}px`;
    this._resizeEl.style.height = newHeightStyle;
    console.log(h, dy, e.y, h-dy);
  }

  _errSizeUp(e) {
    this._resizeEl = null;
    e.currentTarget.releasePointerCapture(e.pointerId);
    e.currentTarget.onpointermove = null;
  }

  _errorGridDown(e) {
    this.model.keepErrorsOpen();
  }

  _errorDetailClick(e) {
    e.currentTarget.classList.toggle('show-detail');
  }

  _closeError() {
    this.model.showErrors = false;
    this.model.showLastError = false;
  }

  connectedCallback() {
    super.connectedCallback();
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this._sessionListObservers.forEach(o => unobserve(o));
    this._removeDialogHandlers(this.renderRoot.getElementById('dlg-filter'));
    this._removeDialogHandlers(this.renderRoot.getElementById('dlg-config'));
  }

  // assumes error is problem details object - see https://tools.ietf.org/html/rfc7807
  defaultHandleError(error) {
    this.model.handleFetchError(error);
  }

  // called at most once every time after connectedCallback was executed
  firstRendered() {
    super.firstRendered();
    this.appTitle = this.getAttribute('appTitle');

    //this._sessionListObservers = this.connectDropdownSessionlist('sessionProfiles', true);
    this._addDialogHandlers(this.renderRoot.getElementById('dlg-filter'));
    this._addDialogHandlers(this.renderRoot.getElementById('dlg-config'));
  }

  rendered() {
    let newHeightStyle = null;
    if (this.model.showErrors) {
      newHeightStyle = `${300}px`;
    } else if (this.model.showLastError) {
      // Calculate the height of the first row in errors, and resize the errors
      // container to show just that row, as it contains the most recent error.
      const topRow = this.renderRoot.querySelector('#error-grid div.kds-row');
      if (topRow) {
        let minTop = 0;
        let maxBottom = 0;
        // topRow has CSS rule "display: contents", so it has no height of its own
        for (let indx = 0; indx < topRow.children.length; indx += 1) {
          const child = topRow.children[indx];
          const ot = child.offsetTop;
          const ob = ot + child.offsetHeight;
          if (ot < minTop) minTop = ot;
          if (ob > maxBottom) maxBottom = ob;
        }
        const topRowHeight = maxBottom - minTop;
        newHeightStyle = `${topRowHeight}px`;
      }
    }

    if (newHeightStyle) {
      const errContainer = this.renderRoot.getElementById('error-resizable');
      errContainer.style.height = newHeightStyle;
    }
  }

  static get styles() {
    return [
      KdSoftGridStyle,
      css`
        :host {
          display: block;
        }
        
        #container {
          position: relative;
          height: 100%;
          display: grid;
          grid-template-columns: minmax(2rem, 1fr) 3fr;
          grid-template-rows: 1fr auto;
        }

        #sidebar {
          grid-column: 1;
          grid-row: 1/2;
          display: flex;
          flex-direction: column;
          flex-wrap: nowrap;
          justify-content: flex-start;
          align-items: stretch;
        }

        #main {
          grid-column: 2;
          grid-row: 1/2;
          height: 100%;
          position: relative;
          display: flex;
          flex-direction: column;
          flex-wrap: nowrap;
          justify-content: flex-start;
          align-items: stretch;
        }

        footer {
          grid-column: 1/-1;
          grid-row: 3;
        }

        .brand {
          font-family: Candara;
        }

        #tab-buttons button {
          padding-left: 0.25rem;
          padding-right: 0.25rem;
        }

        #dlg-config {
          width: 800px;
          min-height: 400px;
          height: 500px;
          max-height: 600px;
          position: relative;
        }

        #dlg-filter {
          width: 80ch;
        }

        #error-resize {
          height: 3px;
          border-top: solid gray 1px;
          border-bottom: solid gray 1px;
          width: 100%;
          cursor: n-resize;
        }

        #error-resizable {
          position: relative;
        }

        #error-grid {
          box-sizing: border-box;
          position: absolute;
          left: 0;
          right: 0;
          top: 0;
          bottom: 0;
          grid-template-columns: fit-content(14em) 1fr;
        }

        #error-close {
          position: sticky;
          top: 0;
          justify-self: end;
          grid-column: 1/-1;
        }

        #error-grid .kds-row > div {
          background-color: #feb2b2;
        }

        #error-grid .kds-row > pre {
          grid-column: 1/-1;
          margin-left: 3em;
          padding: 8px 4px;
          outline: 1px solid #c8c8c8;
          overflow: hidden;
          white-space: nowrap;
          text-overflow: ellipsis;
        }

        #error-grid .kds-row > .show-detail {
          max-height: 300px;
          overflow: scroll;
          white-space: pre;
        }

        #sessionDropDown {
          width: 250px;
        }

        #sessionProfiles {
          width: 275px;
        }
      `
    ];
  }

  /* eslint-disable indent, no-else-return */

  shouldRender() {
    return !!this.model;
  }

  render() {
    const traceSessionList = [...this.model.traceSessions.values()];

    return html`
      ${sharedStyles}
      <link rel="stylesheet" type="text/css" href=${myappStyleLinks.myapp} />
      <link rel="stylesheet" type="text/css" href="css/spinner.css" />
      <style>
        :host {
          position: relative;
        }
      </style>

      <div id="container">

        <nav id="sidebar" class="items-center justify-between text-gray-500 bg-gray-800 pt-2 pb-3 w-full z-30">
          <div class=flex>
            <!-- <div class="pr-2"> -->
              <button id="nav-toggle" @click=${this._toggleNav}
                      class="flex items-center px-3 py-2 text-gray-500 border-gray-600 hover:text-white hover:border-white">
                <i class="fas fa-lg fa-bars"></i>
                <!-- <svg class="fill-current h-3 w-3" viewBox="0 0 20 20" xmlns="http://www.w3.org/2000/svg"><title>Menu</title><path d="M0 3h20v2H0V3zm0 6h20v2H0V9zm0 6h20v2H0v-2z"/></svg> -->
              </button>
            <!-- </div> -->
            <!-- <div class="flex items-center flex-shrink-0 text-white mr-6"> -->
              <a class="text-white no-underline hover:text-white hover:no-underline" href="#">
                <span class="text-2xl pl-2 brand"><i class="brand"></i>KDS</span>
              </a>
            <!-- </div> -->
          </div>
          <div class="flex pr-1 text-white bg-gray-500">
            <label class="pl-3 font-bold text-xl">${i18n.gettext('Profiles')}</label>
            <input id="import-profiles" type="file" @change=${this._importProfilesSelected} multiple class="hidden"></input>
            <button class="px-1 py-1 ml-auto" @click=${this._importProfilesClick} title="Import Profiles"><i class="fas fa-lg fa-file-import"></i></button>
          </div>
          ${this.model.sessionProfiles.map(p => {
            return html`
              <div class="flex flex-wrap">
                <label class="pl-3 font-bold text-xl">${p.name}</label>
                <div class="ml-auto pr-1">
                  <button type="button" class="px-1 py-1" @click=${e => this._openSessionFromProfileClick(e, p)}><i class="fas fa-lg fa-wifi"></i></button>
                  <button type="button" class="px-1 py-1" @click=${e => this._editSessionProfileClick(e, p)}><i class="fas fa-lg fa-edit"></i></button>
                  <button type="button" class="px-1 py-1" @click=${e => this._deleteProfileClick(e, p.name)}><i class="far fa-lg fa-trash-alt"></i></button>
                </div>
              </div>
            `;
          })}
          <div class="flex text-white bg-gray-500">
            <label class="pl-3 font-bold text-xl">${i18n.gettext('Sessions')}</label>
          </div>
          <div>
          ${traceSessionList.map(ses => {
            const eventsClasses = ses.eventSession && ses.eventSession.open ? classList.stopBtn : classList.startBtn;
            return html`
              <kdsoft-tree-node>
                <div slot="content" class="flex flex-wrap">
                  <label class="font-bold text-xl">${ses.name}</label>
                  <div class="ml-auto">
                    <button type="button" class="px-1 py-1" @click=${e => this._showSessionClick(e, ses)}><i class="fas fa-lg fa-eye"></i></button>
                    <button type="button"  class="px-1 py-1" @click=${this._toggleSessionEvents}><i class=${classMap(eventsClasses)}></i></button>
                    <button type="button" class="px-1 py-1 text-gray-500" @click=${e => this._filterSessionClick(e, ses)}><i class="fas fa-filter"></i></button>
                    <button type="button" class="px-1 py-1 text-gray-500" @click=${e => this._closeSessionClick(e, ses)}><i class="far fa-lg fa-trash-alt"></i></button>
                  </div>
                </div>
                <div slot="children">
                  <p class="font-bold">Providers</p>
                  ${ses.state.enabledProviders.map(ep => html`
                    <kdsoft-tree-node>
                      <div slot="content" class="truncate">${ep.name}</div>
                      <div slot="children" style="display:grid;grid-gap:0 1em;grid-template-columns:max-content auto;justify-items: start">
                        <div>Level</div><div>${ep.level}</div>
                        <div>Keywords</div><div>${ep.matchKeywords}</div>
                      </div>
                    </kdsoft-tree-node>
                  `)}
                </div>
              </kdsoft-tree-node>
            `;
          })}
          </div>
          <!-- <kdsoft-checklist id="sessionProfiles" class="text-black leading-normal" .model=${this.model.profileCheckListModel}></kdsoft-checklist> -->
        </nav>

        <div id="main">
          <div id="nav-content" class="lg:flex lg:items-center lg:w-auto hidden lg:block pt-6 lg:pt-0 bg-gray-500">
            <ul class="list-reset lg:flex justify-end flex-1 items-center">
            ${this.model.visibleSessions.map(ses => {
              const isActiveTab = this.model.activeSession === ses;
              const tabClasses = isActiveTab ? classList.tabActive : classList.tabInactive;
              const eventsClasses = ses.eventSession && ses.eventSession.open ? classList.stopBtn : classList.startBtn;

              return html`
                <li class="mr-2 pr-1 ${isActiveTab ? 'bg-gray-700' : ''}" data-session-name=${ses.profile.name.toLowerCase()} @click=${this._sessionTabClick}>
                  <a class=${classMap(tabClasses)} href="#">${ses.profile.name}</a>
                  <div id="tab-buttons" class=${classMap(isActiveTab ? classList.tabButtonsActive : classList.tabButtonsInActive)}>
                    <button type="button" @click=${e => this._toggleSessionEvents(e, ses)}>
                      <i class=${classMap(eventsClasses)}></i>
                    </button>
                    <button type="button" class="text-gray-500" @click=${e => this._filterSessionClick(e, ses)}>
                      <i class="fas fa-filter"></i>
                    </button>
                    <button type="button" class="text-gray-500" @click=${e => this._hideSessionClick(e, ses)}>
                      <i class="fas fa-lg fa-times"></i>
                    </button>
                  </div>
                </li>
                `;
              }
            )}
            </ul>
          </div>

          <!-- Main content -->
          <div class="flex-grow relative">
            <trace-session-view .model=${this.model.activeSession}></trace-session-view>
          </div>
        </div>

        <footer>
          ${(this.model.showLastError || this.model.showErrors)
            ? html`
                <div id="error-resize" @pointerdown=${this._errSizeDown} @pointerup=${this._errSizeUp}></div>
                <div id="error-resizable">
                  <div id="error-grid" class="kds-container px-2 pt-0 pb-2" @pointerdown=${this._errorGridDown}>
                  <button id="error-close" class="p-1 text-gray-500" @click=${this._closeError}>
                    <span aria-hidden="true" class="fas fa-lg fa-times"></span>
                  </button>
                  ${repeat(
                    this.model.fetchErrors.reverseItemIterator(),
                    item => item.sequenceNo,
                    (item, indx) => {
                      return html`
                        <div class="kds-row">
                        <div>${item.timeStamp}</div>
                        <div>${item.title}</div>
                        <pre @click=${this._errorDetailClick}>${item.detail}</pre>
                        </div>
                      `;
                    }
                  )}
                  </div>
                </div>
              `
            : nothing
          }
          <div class="flex p-2 border bg-gray-800 text-white">&copy; Karl Waclawek
            <button class="ml-auto" @click=${this._showErrors}>
              ${this.model.fetchErrors.count()} ${i18n.__('Errors')}
            </button>
          </div>
        </footer>

      </div>

      <dialog id="dlg-config">
        <trace-session-config class="h-full"></trace-session-config>
      </dialog>
      <dialog id="dlg-filter">
        <filter-form></filter-form>
      </dialog>
    `;
  }
}

window.customElements.define('my-app', MyApp);
