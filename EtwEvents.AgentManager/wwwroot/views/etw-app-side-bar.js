/* global i18n */

import { html, nothing } from 'lit';
import { observable, observe, unobserve } from '@nx-js/observer-util';
import { Queue, priorities } from '@nx-js/queue-util';
import dialogPolyfill from 'dialog-polyfill';
import { LitMvvmElement, css, BatchScheduler } from '@kdsoft/lit-mvvm';
import '@kdsoft/lit-mvvm-components/kdsoft-expander.js';
import { KdSoftChecklistModel } from '@kdsoft/lit-mvvm-components';
import checkboxStyles from '@kdsoft/lit-mvvm-components/styles/kdsoft-checkbox-styles.js';
import fontAwesomeStyles from '@kdsoft/lit-mvvm-components/styles/fontawesome/css/all-styles.js';
import tailwindStyles from '../styles/tailwind-styles.js';
import spinnerStyles from '../styles/spinner-styles.js';
import appStyles from '../styles/etw-app-styles.js';
import dialogStyles from '../styles/dialog-polyfill-styles.js';
import '../components/revoked-checklist.js';
import * as utils from '../js/utils.js';

function getAgentIndex(agentList, agentId) {
  return agentList.findIndex(val => val.id === agentId);
}

const dialogClass = utils.html5DialogSupported ? '' : 'fixed';

class EtwAppSideBar extends LitMvvmElement {
  constructor() {
    super();
    // seems priorities.HIGH may not allow render() calls in child components in some scenarios
    //this.scheduler = new Queue(priorities.LOW);
    //this.scheduler = new BatchScheduler(0);
    this.scheduler = window.renderScheduler;
  }

  _toggleNav() {
    const host = this.renderRoot.host;
    const expanded = host.hasAttribute('aria-expanded');
    if (expanded) host.removeAttribute('aria-expanded');
    else host.setAttribute('aria-expanded', 'true');
  }

  _startEvents(currentState) {
    if (currentState && !currentState?.isRunning) this.model.startEvents(currentState);
  }

  _stopEvents(currentState) {
    if (currentState && currentState.isRunning) this.model.stopEvents(currentState);
  }

  _resetAgent(currentState) {
    if (currentState) this.model.resetAgent(currentState);
  }

  _refreshState() {
    const activeAgentState = this.model.activeAgentState;
    this.model.refreshState(activeAgentState);
  }

  _uploadAgentCerts(e) {
    const input = e.currentTarget;
    const data = new FormData();
    for (const file of input.files) {
      data.append(file.name, file);
    }
    this.model.uploadAgentCerts(data)
      .then(() => {
        // clear to prevent change event from not happening
        input.value = null;
      });
  }

  _openRevokeCertDialog(e) {
    const dlg = this.renderRoot.getElementById('dlg-revoke-cert');
    dlg.querySelector('form').reset();
    dlg.showModal();
    this.model.getRevokedCerts()
      .then(certs => {
        if (certs) {
          const revokedList = dlg.querySelector('#revoked-list');
          revokedList.model = observable(new KdSoftChecklistModel(certs));
        }
      });
  }

  _closeRevokeCertDialog(e) {
    const dlg = e.currentTarget.closest('dialog');
    dlg.close();
  }

  _certFilePicked(e) {
    const input = e.currentTarget;
    const selectedFile = input.files[0];

    if (!selectedFile) return;
    if (selectedFile.size > 32758) {
      window.etwApp.defaultHandleError(new Error('Certificate file size too large.'));
      return;
    }

    const fileData = new FormData();
    fileData.append('file', selectedFile, selectedFile.name);
    this.model.getCertInfo(fileData)
      .then(certInfo => {
        if (certInfo) {
          const dlg = this.renderRoot.getElementById('dlg-revoke-cert');
          dlg.querySelector('#thumbprint').value = certInfo.thumbprint;
          dlg.querySelector('#commonName').value = certInfo.name;
        }
      });
  }

  _removeRevokedEntry(e, entry) {
    this.model.cancelCertRevocation(entry.thumbprint)
      .then(certs => {
        if (certs) {
          const dlg = this.renderRoot.getElementById('dlg-revoke-cert');
          const revokedList = dlg.querySelector('#revoked-list');
          revokedList.model.items = certs;
        }
      });
  }

  _revokeCert(e) {
    const dlg = this.renderRoot.getElementById('dlg-revoke-cert');
    const thumbprint = dlg.querySelector('#thumbprint').value;
    const name = dlg.querySelector('#commonName').value;
    this.model.revokeCert(thumbprint, name)
      .then(certs => {
        if (certs) {
          const revokedList = dlg.querySelector('#revoked-list');
          revokedList.model.items = certs;
        }
      });
  }

  _searchTextChanged(e) {
    const searchText = e.currentTarget.value;
    const regex = new RegExp(searchText, 'i');
    this.agentChecklistModel.filter = item => {
      return regex.test(item.state.id);
    };
  }

  //#region overrides

  /* eslint-disable indent, no-else-return */

  disconnectedCallback() {
    if (this._agentListObserver) {
      unobserve(this._agentListObserver);
      this._agentListObserver = null;
    }
    super.disconnectedCallback();
  }

  shouldRender() {
    return !!this.model;
  }

  beforeFirstRender() {
    this.appTitle = this.getAttribute('appTitle');
    if (!this.agentChecklistModel) {
      const agentList = this.model.agents;
      const agentIndex = getAgentIndex(agentList, this.model.activeAgentId);
      const checklistModel = new KdSoftChecklistModel(
        agentList,
        agentIndex >= 0 ? [agentIndex] : [],
        false,
        item => item.id
      );
      this.agentChecklistModel = observable(checklistModel);

      this._agentListObserver = observe(() => {
        const oldActiveAgentId = this.model.activeAgentId;
        const selEntry = this.agentChecklistModel.firstSelectedEntry;
        const selAgent = selEntry?.item;
        this.model.activeAgentId = selAgent?.state.id;
        if (oldActiveAgentId != this.model.activeAgentId) {
          this.model.stopEtwEvents();
        }
      });
    }
  }

  // called at most once every time after connectedCallback was executed
  firstRendered() {
    const eventSinkDlg = this.renderRoot.getElementById('dlg-add-event-sink');
    if (!utils.html5DialogSupported) {
      dialogPolyfill.registerDialog(eventSinkDlg);
    }
  }

  static get styles() {
    return [
      tailwindStyles,
      checkboxStyles,
      fontAwesomeStyles,
      appStyles,
      spinnerStyles,
      utils.html5DialogSupported ? dialogStyles : css``,
      css`
        :host {
          display: block;
          position: relative;
        }

        dialog {
          outline: lightgray solid 1px;
        }

        label {
          font-weight: bolder;
          color: #718096;
        }

        input {
          border-width: 1px;
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
          height: 2em;
          width: 2em;
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

        /* the item template for the checklist contains a part we can select */ 
        #agents::part(content) {
          display: grid;
          grid-gap: 0 1em;
          grid-template-columns: max-content auto;
          justify-items: start;
        }

        #agents::part(header) {
          color: gray;
          background-color: inherit;
        }

        #agents::part(header):hover {
          background-color: inherit;
        }

        #agents::part(header).item-selected {
          background-color: inherit;
        }

        #agents::part(cert-warning) {
          visibility: hidden;
        }

        #agents::part(cert-warning).warning-active {
          visibility: visible;
        }

        .fa-lg.fa-eye, .fa-lg.fa-file-archive {
          min-width: 2em;
        }

        #dlg-revoke-cert form {
          display: grid;
          grid-template-columns: auto auto 3em;
          background: rgba(255,255,255,0.3);
          row-gap: 5px;
          column-gap: 10px;
        }

        #revoke-header {
          grid-column: 1 / -1;
          margin-bottom: .5em;
          display: flex;
        }

        #revoke-header > h3 {
          margin-left: auto;
        }

        #revoke-header > button {
          margin-left: auto;
          color: #718096;
        }

        #revoked-list {
          grid-column: 2 / -1;
          min-height: 2em;
          max-height: 20em;
        }

        #revoke-cert-file {
          grid-column: 1 / -1;
        }

        #revoke-cert-file::file-selector-button {
          display:none;
        }

        #revoke-cert-file::file-selector-button::after {
          content: 'From file';
        }

        #revoke-btn {
          grid-column: 3/-1;
          grid-row: 3/5;
          justify-content: center;
          padding-left: 0.75em;
          padding-right: 0.75em;
        }
      `
    ];
  }

  getAgentTemplate(entry) {
    const onlyModified = entry.modified && !entry.disconnected;
    const playClass = entry.current?.isRunning ? '' : 'text-green-500';
    const stopClass = entry.current?.isRunning ? 'text-red-500' : '';
    const lifeSecs = entry.current?.clientCertLifeSpan?.seconds;
    const clientCertLifeDays = typeof lifeSecs === 'number' ? Math.floor(lifeSecs / 86400) : Number.NaN;
    const clientCertWarningActive = !Number.isNaN(clientCertLifeDays) && (clientCertLifeDays < window.certExpiryWarningDays);
    const clientCertWarning = clientCertWarningActive ? `Agent certificate expires in ${clientCertLifeDays} days` : '';
    // we need to target this class through a part: e.g. "#agents::part(cert-warning)", as it is inside a shadow root
    const warningActive = clientCertWarningActive ? 'warning-active' : '';

    return html`
      <kdsoft-expander class="w-full" .scheduler=${this.scheduler}>
        <div part="header" slot="header" class="flex items-baseline pr-1 text-white bg-gray-500">
          <label class="pl-1 font-bold text-xl">${entry.state.id}</label>
          <span part="cert-warning" class="ml-2 fa fas fa-exclamation-triangle text-red-500 ${warningActive}" title=${clientCertWarning}></span>
          <span class="ml-auto">
            <span class="mr-4">
              ${onlyModified ? html`<button class="mr-1 text-yellow-800 fas fa-pencil-alt"></button>` : nothing}
              ${entry.disconnected ? html`<i class="text-red-800 fas fa-unlink"></i>` : nothing}
            </span>
            <button class="mr-3" @click=${() => this._resetAgent(entry.current)} title="Reset Agent">
              <i class="fas fa-arrow-rotate-left"></i>
            </button>
            <button class="mr-1 ${playClass}" @click=${() => this._startEvents(entry.current)} title="Start Session">
              <i class="fas fa-play"></i>
            </button>
            <button class="mr-1 ${stopClass}" @click=${() => this._stopEvents(entry.current)} title="Stop Session">
              <i class="fas fa-stop"></i>
            </button>
          </span>
        </div>
        <!-- using part="slot" we can style this from here even though it will be rendered inside a web component -->
        <div part="content" slot="content" class="pl-3">
          <label class="pl-1 font-bold">Site</label>
          <div class="pl-1">${entry.state.site}</div>
          <label class="pl-1 font-bold">Host</label>
          <div class="pl-1">${entry.state.host}</div>
          <label class="pl-1 font-bold" title=${entry.current?.clientCertThumbprint}>Certificate</label>
          <div class="pl-1">${Number.isNaN(clientCertLifeDays) ? '??' : clientCertLifeDays } days</div>
        </div>
      </kdsoft-expander>
    `;
  }

  render() {
    const isAdmin = window.userRoles.includes('Admin');
    const uploadLeftMargin = isAdmin ? 'ml-2' : 'ml-auto';
    return html`
      <nav id="sidebar" class="text-gray-500 bg-gray-800 pt-0 pb-3 h-full z-30">
        <!-- <div class="pr-2"> -->
          <button id="nav-toggle"
            @click=${this._toggleNav}
            class="text-gray-600 border-gray-600 hover:text-gray-800"
          >
            <i class="m-auto fas fa-lg fa-bars"></i>
          </button>
        <!-- </div> -->
        <div class="flex pl-8 pr-2">
          <!-- <div class="flex items-center flex-shrink-0 text-white mr-6"> -->
            <a class="text-white no-underline hover:text-white hover:no-underline" href="#">
              <span class="text-2xl pl-2 brand"><i class="brand"></i>KDS</span>
            </a>
            ${isAdmin
              ? html`<button id="revoke-cert-btn" class="flex items-center text-red-500 ml-auto"
                title="Revoke Certificates" @click=${e => this._openRevokeCertDialog(e)}>
                <i class="fas fa-certificate"></i>
              </button>`
              : nothing
            }
            <label for="upload-agent-certs" class="flex items-center text-orange-500 ${uploadLeftMargin}" title="Upload Agent Certificates">
              <i class="fas fa-upload"></i>
              <input id="upload-agent-certs" type="file" class="hidden"
                @change=${e => this._uploadAgentCerts(e)}
                accept=".crt,.pem,.pfx,application/pkix-cert,application/x-pkcs12" multiple />
            </label>
            <button class="text-blue-500 ml-4 fas fa-redo-alt" @click=${() => this._refreshState()} title="Refresh"></button>
          <!-- </div> -->
        </div>

        <input id="agent-search"
          type="text"
          placeholder="search agents"
          class="p-1"
          @input="${this._searchTextChanged}" />
        <etw-checklist id="agents" class="text-black"
          .model=${this.agentChecklistModel}
          .scheduler=${this.scheduler}
          .getItemTemplate=${entry => this.getAgentTemplate(entry)}
        ></etw-checklist>

      </nav>

      <dialog id="dlg-revoke-cert" class="${dialogClass}">
        <form name="revoke-cert" @reset=${e => this._closeRevokeCertDialog(e)}>
          <div id="revoke-header">
            <h3 class="font-bold">Revoke Certificates</h3>
            <button type="reset"><i class="fa-solid fa-lg fa-xmark"></i></button>
          </div>

          <input id="revoke-cert-file" name="formFile"
            type="file" @change=${e => this._certFilePicked(e)}
            accept=".crt,.pem,.pfx,application/pkix-cert,application/x-pkcs12" />
          <label for="thumbprint">Thumbprint</label>
          <input id="thumbprint" name="thumbprint" type="text" />
          <button id="revoke-btn" type="button" @click=${e => this._revokeCert(e)}
            class="flex items-center text-red-500 ml-auto" title="Revoke">
            <i class="fa-solid fa-2xl fa-ban"></i>
          </button>
          <label for="commonName">Common Name</label>
          <input id="commonName" name="commonName" type="text" />

          <label for="revoked-list">Revoked Certificates</label>
          <revoked-checklist
            id="revoked-list" 
            class="text-black" 
            .getItemTemplate=${item => html`
              <div class="flex w-full revoked-entry">
                <span class="mr-2">${item.name}</span>
                <span class="thumb-print ml-auto">(<i title=${item.thumbprint}>${item.thumbprint}</i>)</span>
                <button class="fa-solid fa-lg fa-times self-center ml-2" @click=${e => this._removeRevokedEntry(e, item)}></button>
              </div>`
            }
            .attachInternals=${true}
            tabindex=-1>
          </revoked-checklist>
        </form>
      </dialog>

    `;
  }

  //#endregion
}

window.customElements.define('etw-app-side-bar', EtwAppSideBar);
