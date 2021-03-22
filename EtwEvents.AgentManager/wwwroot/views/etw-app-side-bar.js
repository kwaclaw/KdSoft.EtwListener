/* global i18n */

import { html, nothing } from '../lib/lit-html.js';
import { classMap } from '../lib/lit-html/directives/class-map.js';
import { observe } from '../lib/@nx-js/observer-util/dist/es.es6.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util/dist/es.es6.js';
import { LitMvvmElement, css } from '../lib/@kdsoft/lit-mvvm.js';
import dialogPolyfill from '../lib/dialog-polyfill.js';
import FilterFormModel from './filter-form-model.js';
import '../components/kdsoft-expander.js';
import './filter-form.js';
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

const dialogClass = utils.html5DialogSupported ? '' : 'fixed';

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

function getAgentIndex(agentList, agentName) {
  return agentList.findIndex(val => val.name === agentName);
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
  }

  shouldRender() {
    return !!this.model;
  }

  // called at most once every time after connectedCallback was executed
  beforeFirstRender() {
    this.appTitle = this.getAttribute('appTitle');
    if (!this.agentChecklistModel) {
      const agentList = Array.from(this.model.agents.values());
      const agentIndex = getAgentIndex(agentList, this.model.activeAgentName);
      this.agentChecklistModel = new KdSoftChecklistModel(
        agentList,
        agentIndex >= 0 ? [agentIndex] : [],
        false,
        item => item.name
      );
    }
  }

  firstRendered() {
    const filterDlg = this.renderRoot.getElementById('dlg-filter');
    const configDlg = this.renderRoot.getElementById('dlg-config');

    if (!utils.html5DialogSupported) {
      dialogPolyfill.registerDialog(filterDlg);
      dialogPolyfill.registerDialog(configDlg);
    }

    this._addDialogHandlers(filterDlg);
    this._addDialogHandlers(configDlg);
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
          color: inherit;
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
            <!-- <svg class="fill-current h-3 w-3" viewBox="0 0 20 20"
              xmlns="http://www.w3.org/2000/svg"><title>Menu</title><path d="M0 3h20v2H0V3zm0 6h20v2H0V9zm0 6h20v2H0v-2z"/>
            </svg> -->
          </button>
        <!-- </div> -->
        <div class="flex pl-8">
          <!-- <div class="flex items-center flex-shrink-0 text-white mr-6"> -->
            <a class="text-white no-underline hover:text-white hover:no-underline" href="#">
              <span class="text-2xl pl-2 brand"><i class="brand"></i>KDS</span>
            </a>
          <!-- </div> -->
        </div>

        <kdsoft-checklist id="agents" class="text-black"
          .model=${this.agentChecklistModel}
          .getItemTemplate=${item => html`${item.name}`}
          show-checkboxes>
        </kdsoft-checklist>

      </nav>

      <dialog id="dlg-config" class="${dialogClass}">
        <trace-session-config class="h-full"></trace-session-config>
      </dialog>
      <dialog id="dlg-filter" class="${dialogClass}">
        <filter-form></filter-form>
      </dialog>
    `;
  }

  //#endregion
}

window.customElements.define('etw-app-side-bar', EtwAppSideBar);
