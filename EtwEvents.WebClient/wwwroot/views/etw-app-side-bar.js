/* global i18n */

import { html, nothing } from '../lib/lit-html.js';
import { classMap } from '../lib/lit-html/directives/class-map.js';
import { observe } from '../lib/@nx-js/observer-util/dist/es.es6.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util/dist/es.es6.js';
import { LitMvvmElement, css } from '../lib/@kdsoft/lit-mvvm.js';
import dialogPolyfill from '../lib/dialog-polyfill.js';
import FilterFormModel from './filter-form-model.js';
import '../components/kdsoft-expander.js';
import './trace-session-config.js';
import './filter-form.js';
import './event-sink-config.js';
import TraceSessionConfigModel from './trace-session-config-model.js';
import TraceSessionProfile from '../js/traceSessionProfile.js';
import EventSinkConfigModel from './event-sink-config-model.js';
import KdSoftChecklistModel from '../components/kdsoft-checklist-model.js';
import Spinner from '../js/spinner.js';
import * as utils from '../js/utils.js';
import sharedStyles from '../styles/kdsoft-shared-styles.js';
import { KdSoftGridStyle } from '../styles/kdsoft-grid-style.js';
import styleLinks from '../styles/kdsoft-style-links.js';
import etwAppStyleLinks from '../styles/etw-app-style-links.js';

const runBtnBase = { fas: true };

const classList = {
  startBtnActive: { ...runBtnBase, 'fa-play': true, 'text-green-500': true },
  startBtnInactive: { ...runBtnBase, 'fa-play': true },
  stopBtn: { ...runBtnBase, 'fa-stop': true, 'text-red-500': true },
};

function formDoneHandler(e) {
  if (!e.detail.canceled) {
    if (e.target.localName === 'filter-form') {
      this.model.saveSessionProfile(e.detail.model.session.profile);
    } else if (e.target.localName === 'trace-session-config') {
      this.model.saveSessionProfile(e.detail.model.cloneAsProfile());
    } else if (e.target.localName === 'event-sink-config') {
      this.model.saveSinkProfile(e.detail.model);
    }
  }

  const dlg = e.currentTarget;
  dlg.close();
}

function formSaveHandler(e) {
  if (e.target.localName === 'filter-form') {
    this.model.saveSessionProfile(e.detail.model.session.profile);
  } else if (e.target.localName === 'trace-session-config') {
    this.model.saveSessionProfile(e.detail.model.cloneAsProfile());
  } else if (e.target.localName === 'event-sink-config') {
    this.model.saveSinkProfile(e.detail.model);
  }
}

class EtwAppSideBar extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);

    // this allows us to unregister the event handlers, because we maintain references to their instances
    this._formDoneHandler = formDoneHandler.bind(this);
    this._formSaveHandler = formSaveHandler.bind(this);
    this._dialogFocusOut = {
      handleEvent(e) {
        e.target.closest('dialog').close();
      },
      capture: true,
    };
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

  //#region session profile

  _showSessionProfileDialog(profile) {
    const configModel = new TraceSessionConfigModel(profile);

    const dlg = this.renderRoot.getElementById('dlg-config');
    const cfg = dlg.getElementsByTagName('trace-session-config')[0];
    cfg.model = configModel;
    dlg.showModal();
  }

  _addSessionProfileClick(e) {
    this._showSessionProfileDialog(new TraceSessionProfile('<New Session Profile>'));
  }

  _editSessionProfileClick(e, profile) {
    this._showSessionProfileDialog(profile);
  }

  _importSessionProfilesClick() {
    const fileDlg = this.renderRoot.getElementById('import-session-profiles');
    fileDlg.click();
  }

  _importSessionProfilesSelected(e) {
    this.model.importSessionProfiles(e.currentTarget.files);
  }

  _deleteSessionProfileClick(e, profileName) {
    e.stopPropagation();
    this.model.deleteSessionProfile(profileName.toLowerCase());
  }

  //#endregion

  //#region event sink profile

  async _showSinkProfileDialog(sinkProfile) {
    const configModel = await EventSinkConfigModel.create(sinkProfile);

    const dlg = this.renderRoot.getElementById('dlg-event-sink');
    const cfg = dlg.querySelector('event-sink-config');
    cfg.model = configModel;
    dlg.showModal();
  }

  async _addEventSinkProfileClick(e) {
    await this._showSinkProfileDialog(null);
  }

  async _editEventSinkProfileClick(e, sinkProfile) {
    await this._showSinkProfileDialog(sinkProfile);
  }

  _importEventSinkProfilesClick() {
    const fileDlg = this.renderRoot.getElementById('import-sink-profiles');
    fileDlg.click();
  }

  _importEventSinkProfilesSelected(e) {
    this.model.importSinkProfiles(e.currentTarget.files);
  }

  _deleteEventSinkProfileClick(e, sinkDefinitionName) {
    e.stopPropagation();
    this.model.deleteSinkProfile(sinkDefinitionName.toLowerCase());
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

  _chooseEventSinkClick(e, session) {
    const checklist = this.renderRoot.getElementById('eventSinkProfileList');
    const model = new KdSoftChecklistModel(this.model.eventSinkProfiles, [], false, item => item.name);
    checklist.model = model;

    const dlg = this.renderRoot.getElementById('dlg-event-sink-chooser');
    const openButton = e.currentTarget;
    model.observer = observe(() => {
      dlg.close();
      const selectedSinkProfile = model.firstSelectedEntry;
      if (selectedSinkProfile && session) {
        const spinner = new Spinner(openButton);
        session.openEventSink(selectedSinkProfile, spinner);
      }
    });

    // dlg positioned in relation to :host (renderRoot)
    const containerTop = utils.containerOffsetTop(this.renderRoot.host, e.currentTarget);
    const containerLeft = utils.containerOffsetLeft(this.renderRoot.host, e.currentTarget);
    const topEdge = containerTop + e.currentTarget.offsetHeight;
    const rightEdge = this.renderRoot.host.clientWidth - (containerLeft + e.currentTarget.offsetWidth);
    dlg.style.top = `${topEdge}px`;
    dlg.style.right = `${rightEdge}px`;
    //dlg.addEventListener('click', e => console.log(e.currentTarget));
    dlg.show();
  }

  _closeEventSinkClick(e, session, eventSink) {
    const closeButton = e.currentTarget;
    const spinner = new Spinner(closeButton);
    session.closeEventSink(eventSink.name, spinner);
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

  /* eslint-disable indent, no-else-return */

  disconnectedCallback() {
    super.disconnectedCallback();

    this.model.unobserveVisibleSessions();

    this._removeDialogHandlers(this.renderRoot.getElementById('dlg-filter'));
    this._removeDialogHandlers(this.renderRoot.getElementById('dlg-config'));
    this._removeDialogHandlers(this.renderRoot.getElementById('dlg-event-sink'));
  }

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

  static get styles() {
    return [
      KdSoftGridStyle,
      css`
        :host {
          display: block;
        }

        dialog {
          outline: lightgray solid 1px;
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
          /* height: 500px; */
          max-height: 800px;
        }

        #dlg-event-sink-chooser {
          color: #a0aec0;
          background-color: #718096;
          left: unset;
          margin: 0;
        }

        #sessionProfiles {
          width: 275px;
        }

        kdsoft-expander.session-details [slot="content"] {
          display: grid;
          grid-gap: 0 1em;
          grid-template-columns: max-content auto;
          justify-items: start;
        }

        .fa-lg.fa-eye, .fa-lg.fa-file-archive {
          min-width: 2em;
        }
      `
    ];
  }

  render() {
    const traceSessionList = [...this.model.traceSessions.values()];
    const dialogStyle = utils.html5DialogSupported
      ? nothing
      : html`<link rel="stylesheet" type="text/css" href=${styleLinks.dialog} />`;


    return html`
      ${sharedStyles}
      ${dialogStyle}
      <link rel="stylesheet" type="text/css" href=${etwAppStyleLinks.etwApp} />
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

        <kdsoft-expander>
          <div slot="header" class="flex pr-1 text-white bg-gray-500">
            <label class="pl-3 font-bold text-xl">${i18n.gettext('Session Profiles')}</label>
            <button type="button" class="px-1 py-1 ml-auto" @click=${e => this._addSessionProfileClick(e)}>
              <i class="fas fa-lg fa-plus"></i>
            </button>
            <input id="import-session-profiles"
              type="file"
              @change=${this._importSessionProfilesSelected}
              multiple
              class="hidden">
            </input>
            <button class="px-1 py-1" @click=${this._importSessionProfilesClick} title="${i18n.gettext('Import Session Profiles')}">
              <i class="fas fa-lg fa-file-import"></i>
            </button>
          </div>
          <div slot="content">
            ${this.model.sessionProfiles.map(p => html`
                <div class="flex flex-wrap">
                  <label class="pl-3 font-bold text-xl">${p.name}</label>
                  <div class="ml-auto pr-1">
                    <button type="button" class="px-1 py-1" @click=${e => this._openSessionFromProfileClick(e, p)}>
                      <i class="fas fa-lg fa-wifi"></i>
                    </button>
                    <button type="button" class="px-1 py-1" @click=${e => this._editSessionProfileClick(e, p)}>
                      <i class="fas fa-lg fa-edit"></i>
                    </button>
                    <button type="button" class="px-1 py-1" @click=${e => this._deleteSessionProfileClick(e, p.name)}>
                      <i class="far fa-lg fa-trash-alt"></i>
                    </button>
                  </div>
                </div>
              `)
            }
          </div>
        </kdsoft-expander>

        <kdsoft-expander>
          <div slot="header" class="flex pr-1 text-white bg-gray-500">
            <label class="pl-3 font-bold text-xl">${i18n.gettext('Event Sink Definitions')}</label>
            <button type="button" class="px-1 py-1 ml-auto" @click=${e => this._addEventSinkProfileClick(e)}>
              <i class="fas fa-lg fa-plus"></i>
            </button>
            <input id="import-sink-profiles"
              type="file"
              @change=${this._importEventSinkProfilesSelected}
              multiple
              class="hidden">
            </input>
            <button class="px-1 py-1" @click=${this._importEventSinkProfilesClick} title="${i18n.gettext('Import Event Sinks')}">
              <i class="fas fa-lg fa-file-import">
            </i></button>
          </div>
          <div slot="content">
            ${this.model.eventSinkProfiles.map(p => html`
                <div class="flex flex-wrap">
                  <label class="pl-3 font-bold text-xl">${p.name}</label>
                  <div class="ml-auto pr-1">
                    <button type="button" class="px-1 py-1" @click=${e => this._editEventSinkProfileClick(e, p)}>
                      <i class="fas fa-lg fa-edit"></i>
                    </button>
                    <button type="button" class="px-1 py-1" @click=${e => this._deleteEventSinkProfileClick(e, p.name)}>
                      <i class="far fa-lg fa-trash-alt">
                    </i></button>
                  </div>
                </div>
              `)
            }
          </div>
        </kdsoft-expander>
        
        <kdsoft-expander>
          <div slot="header" class="flex text-white bg-gray-500">
            <label class="pl-3 font-bold text-xl">${i18n.gettext('Sessions')}</label>
          </div>
          <div slot="content">
            ${traceSessionList.map(ses => {
              const eventsClasses = ses.state.isRunning
                ? classList.stopBtn
                : ses.state.isStopped ? classList.startBtnInactive : classList.startBtnActive;
              return html`
                <kdsoft-expander>
                  <div slot="header" class="flex flex-wrap">
                    <label class="font-bold text-xl">${ses.name}</label>
                    <div class="ml-auto">
                      <button type="button" class="px-1 py-1" @click=${e => this._watchSessionClick(e, ses)}>
                        <i class="fas fa-lg fa-eye"></i>
                      </button>
                      <button type="button"  class="px-1 py-1" @click=${e => this._toggleSessionEvents(e, ses)}>
                        <i class=${classMap(eventsClasses)}></i>
                      </button>
                      <button type="button" class="px-1 py-1 text-gray-500" @click=${e => this._filterSessionClick(e, ses)}>
                        <i class="fas fa-filter"></i>
                      </button>
                      <button type="button" class="px-1 py-1 text-gray-500" @click=${e => this._closeSessionClick(e, ses)}>
                        <i class="fas fa-lg fa-times"></i>
                      </button>
                    </div>
                  </div>
                  <div slot="content">
                    <div class="flex">
                      <label class="font-bold">${i18n.gettext('Event Sinks')}</label>
                      <button class="px-1 py-1 ml-auto" @click=${e => this._chooseEventSinkClick(e, ses)} title="Open Event Sink">
                        <i class="fas fa-lg fa-plus"></i>
                      </button>
                    </div>
                    ${ses.state.eventSinks.map(evs => {
                      const evsType = evs.error
                        ? i18n.gettext('Failed')
                        : (evs.isLocal ? i18n.gettext('Local') : i18n.gettext('External'));
                      const evsColor = evs.error ? 'text-red-500' : (evs.isLocal ? 'text-blue-500' : 'inherited');
                      const evsIcon = evs.isLocal ? 'fa-eye' : 'fa-file-archive ml-1';
                      return html`
                        <kdsoft-expander class="session-details">
                          <div slot="header" class="flex flex-wrap items-center">
                            <label class="truncate ${evsColor}"><i class="fas fa-lg ${evsIcon}"></i>${evs.name}</label>
                            <div class="ml-auto">
                              <button type="button"
                                class="px-1 py-1 text-gray-500"
                                @click=${e => this._closeEventSinkClick(e, ses, evs)}>
                                <i class="fas fa-lg fa-times"></i>
                              </button>
                            </div>
                          </div>
                          <div slot="content">
                            <div>Type</div><div class="ml-4 ${evsColor}">${evsType}</div>
                            ${evs.error ? html`<div>Error</div><div class="ml-4">${evs.error}</div>` : nothing}
                          </div>
                        </kdsoft-expander>
                      `;
                    })}
                    <p class="font-bold mt-3">Providers</p>
                    ${ses.state.enabledProviders.map(ep => html`
                      <kdsoft-expander class="session-details">
                        <div slot="header" class="truncate">${ep.name}</div>
                        <div slot="content">
                          <div>Level</div><div>${ep.level}</div>
                          <div>Keywords</div><div>${ep.matchKeywords}</div>
                        </div>
                      </kdsoft-expander>
                    `)}
                  </div>
              </kdsoft-expander>
            `;
          })}
        </div>
        </kdsoft-expander>
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
      <dialog id="dlg-event-sink-chooser" class="${utils.html5DialogSupported ? '' : 'fixed'}" @focusout=${this._dialogFocusOut}>
        <h3 class="mb-3">Open Event Sink</h3>
        <kdsoft-checklist
          id="eventSinkProfileList"
          .getItemTemplate=${item => html`${item.name}`}>
        </kdsoft-checklist>
      </dialog>
    `;
  }

  //#endregion
}

window.customElements.define('etw-app-side-bar', EtwAppSideBar);
