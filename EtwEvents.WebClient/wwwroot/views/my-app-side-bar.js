/* global i18n */

import { html, nothing } from '../lib/lit-html.js';
import { classMap } from '../lib/lit-html/directives/class-map.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { LitMvvmElement, css } from '../lib/@kdsoft/lit-mvvm.js';
import dialogPolyfill from '../lib/dialog-polyfill.js';
import FilterFormModel from './filter-form-model.js';
import './kdsoft-tree-node.js';
import './trace-session-config.js';
import './filter-form.js';
import './event-sink-config.js';
import TraceSessionConfigModel from './trace-session-config-model.js';
import EventSinkConfigModel from './event-sink-config-model.js';
import Spinner from '../js/spinner.js';
import * as utils from '../js/utils.js';
import sharedStyles from '../styles/kdsoft-shared-styles.js';
import { KdSoftGridStyle } from '../styles/kdsoft-grid-style.js';
import styleLinks from '../styles/kdsoft-style-links.js';
import myappStyleLinks from '../styles/my-app-style-links.js';

const runBtnBase = { fas: true };

const classList = {
  startBtn: { ...runBtnBase, 'fa-play': true, 'text-green-500': true },
  stopBtn: { ...runBtnBase, 'fa-stop': true, 'text-red-500': true },
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


class MyAppSideBar extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);

    // this allows us to unregister the event handlers, because we maintain references to their instances
    this._formDoneHandler = formDoneHandler.bind(this);
    this._formSaveHandler = formSaveHandler.bind(this);
  }

  showFilterDlg(session) {
    const dlg = this.renderRoot.getElementById('dlg-filter');
    const cfg = dlg.getElementsByTagName('filter-form')[0];
    cfg.model = new FilterFormModel(session);
    dlg.showModal();
  }

  _toggleNav() {
    const host = this.renderRoot.host;
    const expanded = host.hasAttribute('aria-expanded');
    if (expanded) host.removeAttribute('aria-expanded');
    else host.setAttribute('aria-expanded', 'true');
  }

  //#region profile

  _editSessionProfileClick(e, profile) {
    const configModel = new TraceSessionConfigModel(profile);

    const dlg = this.renderRoot.getElementById('dlg-config');
    const cfg = dlg.getElementsByTagName('trace-session-config')[0];
    cfg.model = configModel;
    dlg.showModal();
  }

  _importProfilesClick() {
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

  //#endregion

  //#region session

  async _openSessionFromProfileClick(e, profile) {
    const spinner = new Spinner(e.currentTarget);
    await this.model.openSessionFromProfile(profile, spinner);
  }

  async _closeSessionClick(e, session) {
    if (!session) return;

    const spinner = new Spinner(e.currentTarget);
    await this.model.closeSession(session, spinner);
  }

  _watchSessionClick(e, session) {
    if (!session) return;
    this.model.watchSession(session);
  }

  _unwatchSessionClick(e, session) {
    if (!session) return;
    this.model.unwatchSession(session);
  }

  _filterSessionClick(e, session) {
    if (!session) return;
    this.showFilterDlg(session);
  }

  _toggleSessionEvents(e, session) {
    if (!session) return;
    const spinner = new Spinner(e.currentTarget);
    session.toggleEvents(spinner);
  }

  _observeSessionEvents(e, session) {
    if (!session) return;
    session.observeEvents();
  }

  //#endregion

  //#region event sinks

  _openEventSinkClick(e, session) {
     const spinner = new Spinner(e.currentTarget);
     session.openEventSink(spinner);

    //const configModel = new EventSinkConfigModel(0);

    //const dlg = this.renderRoot.getElementById('dlg-event-sink');
    //const cfg = dlg.querySelector('event-sink-config');
    //cfg.model = configModel;
    //dlg.showModal();
  }

  //#endregion

  //#region overrides

  _addDialogHandlers(dlg) {
    dlg.addEventListener('kdsoft-done', this._formDoneHandler);
    dlg.addEventListener('kdsoft-save', this._formSaveHandler);
  }

  _removeDialogHandlers(dlg) {
    dlg.removeEventListener('kdsoft-done', this._formDoneHandler);
    dlg.removeEventListener('kdsoft-save', this._formSaveHandler);
  }

  connectedCallback() {
    super.connectedCallback();
  }

  disconnectedCallback() {
    super.disconnectedCallback();

    this.model.unobserveVisibleSessions();

    this._removeDialogHandlers(this.renderRoot.getElementById('dlg-filter'));
    this._removeDialogHandlers(this.renderRoot.getElementById('dlg-config'));
    this._removeDialogHandlers(this.renderRoot.getElementById('dlg-event-sink'));
  }

  static get styles() {
    return [
      KdSoftGridStyle,
      css`
        :host {
          display: block;
        }
        
        #sidebar {
          display: flex;
          flex-direction: column;
          flex-wrap: nowrap;
          justify-content: flex-start;
          align-items: stretch;
          width: 100%;
          min-width: 8rem;
        }

        #nav-toggle {
          position: fixed;
          top:0;
          left: 0;
          display: flex;
          align-items: center;
          background-color: transparent;
          z-index: 30;
        }

        #nav-toggle:focus {
          outline: none;
        }

        :host([aria-expanded]) #nav-toggle {
          color: #a0aec0;
          border-color: #718096;
        }
        
        :host([aria-expanded]) #nav-toggle:hover {
          color: white;
          border-color: white;
        }
        
        .brand {
          font-family: Candara;
        }

        #dlg-config {
          width: 800px;
          min-height: 400px;
          height: 500px;
          max-height: 600px;
        }
        
        #dlg-filter {
          width: 80ch;
        }

        #dlg-event-sink {
          width: 800px;
          min-height: 400px;
          height: 500px;
          max-height: 600px;
        }

        #sessionProfiles {
          width: 275px;
        }

        kdsoft-tree-node.session-details [slot="children"] {
          display: grid;
          grid-gap: 0 1em;
          grid-template-columns: max-content auto;
          justify-items: start;
        }
      `
    ];
  }

  /* eslint-disable indent, no-else-return */

  shouldRender() {
    return !!this.model;
  }

  // called at most once every time after connectedCallback was executed
  beforeFirstRender() {
    this.appTitle = this.getAttribute('appTitle');
    this.model.observeVisibleSessions();
  }

  firstRendered() {
    const filterDlg = this.renderRoot.getElementById('dlg-filter');
    const configDlg = this.renderRoot.getElementById('dlg-config');
    const eventSinkDlg = this.renderRoot.getElementById('dlg-event-sink');

    if (!utils.html5DialogSupported) {
      dialogPolyfill.registerDialog(filterDlg);
      dialogPolyfill.registerDialog(configDlg);
      dialogPolyfill.registerDialog(eventSinkDlg);
    }

    this._addDialogHandlers(filterDlg);
    this._addDialogHandlers(configDlg);
    this._addDialogHandlers(eventSinkDlg);
  }

  render() {
    const traceSessionList = [...this.model.traceSessions.values()];
    const dialogStyle = utils.html5DialogSupported
      ? nothing
      : html`<link rel="stylesheet" type="text/css" href=${styleLinks.dialog} />`;

    return html`
      ${sharedStyles}
      ${dialogStyle}
      <link rel="stylesheet" type="text/css" href=${myappStyleLinks.myapp} />
      <link rel="stylesheet" type="text/css" href="css/spinner.css" />
      <style>
        :host {
          position: relative;
        }
      </style>
      <nav id="sidebar" class="text-gray-500 bg-gray-800 pt-0 pb-3 h-full z-30">
        <!-- <div class="pr-2"> -->
          <button id="nav-toggle" @click=${this._toggleNav} class="px-3 py-3 text-gray-600 border-gray-600 hover:text-gray-800">
            <i class="fas fa-lg fa-bars"></i>
            <!-- <svg class="fill-current h-3 w-3" viewBox="0 0 20 20" xmlns="http://www.w3.org/2000/svg"><title>Menu</title><path d="M0 3h20v2H0V3zm0 6h20v2H0V9zm0 6h20v2H0v-2z"/></svg> -->
          </button>
        <!-- </div> -->
        <div class="flex pl-8">
          <!-- <div class="flex items-center flex-shrink-0 text-white mr-6"> -->
            <a class="text-white no-underline hover:text-white hover:no-underline" href="#">
              <span class="text-2xl pl-2 brand"><i class="brand"></i>KDS</span>
            </a>
          <!-- </div> -->
        </div>
        <div class="flex pr-1 text-white bg-gray-500">
          <label class="pl-3 font-bold text-xl">${i18n.gettext('Session Profiles')}</label>
          <input id="import-profiles" type="file" @change=${this._importProfilesSelected} multiple class="hidden"></input>
          <button class="px-1 py-1 ml-auto" @click=${this._importProfilesClick} title="Import Profiles"><i class="fas fa-lg fa-file-import"></i></button>
        </div>
        ${this.model.sessionProfiles.map(p => html`
            <div class="flex flex-wrap">
              <label class="pl-3 font-bold text-xl">${p.name}</label>
              <div class="ml-auto pr-1">
                <button type="button" class="px-1 py-1" @click=${e => this._openSessionFromProfileClick(e, p)}><i class="fas fa-lg fa-wifi"></i></button>
                <button type="button" class="px-1 py-1" @click=${e => this._editSessionProfileClick(e, p)}><i class="fas fa-lg fa-edit"></i></button>
                <button type="button" class="px-1 py-1" @click=${e => this._deleteProfileClick(e, p.name)}><i class="far fa-lg fa-trash-alt"></i></button>
              </div>
            </div>
          `)
        }
        <div class="flex text-white bg-gray-500">
          <label class="pl-3 font-bold text-xl">${i18n.gettext('Sessions')}</label>
        </div>
        <div>
        ${traceSessionList.map(ses => {
          const eventsClasses = ses.state.isRunning ? classList.stopBtn : classList.startBtn;
          return html`
            <kdsoft-tree-node>
              <div slot="content" class="flex flex-wrap">
                <label class="font-bold text-xl">${ses.name}</label>
                <div class="ml-auto">
                  <button type="button" class="px-1 py-1" @click=${e => this._watchSessionClick(e, ses)}><i class="fas fa-lg fa-eye"></i></button>
                  <button type="button"  class="px-1 py-1" @click=${e => this._toggleSessionEvents(e, ses)}><i class=${classMap(eventsClasses)}></i></button>
                  <button type="button" class="px-1 py-1 text-gray-500" @click=${e => this._filterSessionClick(e, ses)}><i class="fas fa-filter"></i></button>
                  <button type="button" class="px-1 py-1 text-gray-500" @click=${e => this._closeSessionClick(e, ses)}><i class="far fa-lg fa-trash-alt"></i></button>
                </div>
              </div>
              <div slot="children">
                <div class="flex">
                  <label class="font-bold">${i18n.gettext('Event Sinks')}</label>
                   <button class="px-1 py-1 ml-auto" @click=${e => this._openEventSinkClick(e, ses)} title="Open Event Sink"><i class="fas fa-lg fa-plus"></i></button>
                </div>
                ${ses.state.eventSinks.map(ev => {
                  const evsType = ev.error ? i18n.gettext('Failed') : (ev.isLocal ? i18n.gettext('Local') : i18n.gettext('External'));
                  const evsColor = ev.error ? 'text-red-500' : (ev.isLocal ? 'text-blue-500' : 'inherited');
                  return html`
                    <kdsoft-tree-node class="session-details">
                      <div slot="content" class="truncate ${evsColor}">
                        <i class="fas fa-lg fa-eye"></i> ${evsType}
                      </div>
                      <div slot="children">
                        <div>Name</div><div>${ev.name}</div>
                        ${ev.error ? html`<div>Error</div><div>${ev.error}</div>` : nothing}
                      </div>
                    </kdsoft-tree-node>
                  `;
                })}
                <p class="font-bold mt-3">Providers</p>
                ${ses.state.enabledProviders.map(ep => html`
                  <kdsoft-tree-node class="session-details">
                    <div slot="content" class="truncate">${ep.name}</div>
                    <div slot="children">
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
      </nav>

      <dialog id="dlg-config" class="${utils.html5DialogSupported ? '' : 'fixed'}">
        <trace-session-config class="h-full"></trace-session-config>
      </dialog>
      <dialog id="dlg-filter" class="${utils.html5DialogSupported ? '' : 'fixed'}">
        <filter-form></filter-form>
      </dialog>
      <dialog id="dlg-event-sink" class="${utils.html5DialogSupported ? '' : 'fixed'}">
        <event-sink-config></event-sink-config>
      </dialog>
    `;
  }
}

window.customElements.define('my-app-side-bar', MyAppSideBar);
