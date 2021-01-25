import { html, nothing } from '../lib/lit-html.js';
import { LitMvvmElement, css } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util/dist/es.es6.js';
import { observable, observe, unobserve, raw } from '../lib/@nx-js/observer-util/dist/es.es6.js';
import * as utils from '../js/utils.js';
import sharedStyles from '../styles/kdsoft-shared-styles.js';
import styleLinks from '../styles/kdsoft-style-links.js';
import '../components/kdsoft-dropdown.js';
import '../components/kdsoft-checklist.js';
import '../components/kdsoft-expander.js';
import '../components/kdsoft-drop-target.js';
import '../components/kdsoft-tree-view.js';
import EventSinkProfile from '../js/eventSinkProfile.js';

async function loadSinkDefinitionTemplate(sinkType) {
  const elementModule = await import(sinkType.configViewUrl);
  const configElement = elementModule.default;
  return configElement;
}

async function loadSinkDefinitionModel(sinkType) {
  const modelModule = await import(sinkType.configModelUrl);
  const modelClass = modelModule.default;
  return new modelClass();
}

function getSelectedSinkTypeText(checkListModel) {
  let result = null;
  for (const selEntry of checkListModel.selectedEntries) {
    if (result) result += `, ${selEntry.item.name}`;
    else result = selEntry.item.name;
  }
  return result;
}

class EventSinkConfig extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
    // for "nothing" to work we need to render raw(this.sinkTypeTemplateHolder.value)
    this.sinkTypeTemplateHolder = observable({ tag: nothing });
  }

  _cancel() {
    const container = this.renderRoot.getElementById('container');
    const model = container ? container.children[0].model : null;
    const evt = new CustomEvent('kdsoft-done', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model, canceled: true }
    });
    this.dispatchEvent(evt);

  }

  _apply(e) {
    const container = this.renderRoot.getElementById('container');
    const configElement = container.children[0];
    if (!configElement.isValid()) return;

    const evt = new CustomEvent('kdsoft-done', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model: configElement.model, canceled: false }
    });
    this.dispatchEvent(evt);
  }

  _isValid() {
    return this.renderRoot.querySelector('form').reportValidity();
  }

  async _loadConfigComponent() {
    const sinkType = this.model.selectedSinkType;
    if (sinkType) {
      try {
        const configFormTemplate = await loadSinkDefinitionTemplate(sinkType);
        const configModel = await loadSinkDefinitionModel(sinkType);

        const sinkProfile = this.model.sinkProfile;
        if (sinkProfile) {
          utils.setTargetProperties(configModel, sinkProfile);
        } else {
          const nameInput = this.renderRoot.getElementById('sinkDefinitionName');
          configModel.name = nameInput.value;
          configModel.type = sinkType.value;
        }

        this.sinkTypeTemplateHolder.tag = configFormTemplate(configModel);
      } catch(error) {
        // for "nothing" to work we need to render raw(this.sinkTypeTemplateHolder.value)
        this.sinkTypeTemplateHolder.tag = nothing;
        window.etwApp.defaultHandleError(error);
      }
    } else {
      this.sinkTypeTemplateHolder.tag = nothing;
    }
  }

  async _continue() {
    if (this._isValid()) await this._loadConfigComponent();
  }

  _export() {
    const container = this.renderRoot.getElementById('container');
    const sinkConfigModel = container.children[0].model;

    const profileToExport = new EventSinkProfile(sinkConfigModel.name, sinkConfigModel.type);
    profileToExport.definition = sinkConfigModel.definition;
    const profileString = JSON.stringify(profileToExport, null, 2);
    const profileURL = `data:text/plain,${profileString}`;

    const a = document.createElement('a');
    try {
      a.style.display = 'none';
      a.href = profileURL;
      a.download = `${profileToExport.name}.json`;
      document.body.appendChild(a);
      a.click();
    } finally {
      document.body.removeChild(a);
    }
  }

  /* eslint-disable indent, no-else-return */

  shouldRender() {
    const result = !!this.model;
    return result;
  }

  async beforeFirstRender() {
    // model is defined, because of our shouldRender() override
    if (this.model.sinkProfile) {
      await this._loadConfigComponent();
    } else {
      this.sinkTypeTemplateHolder.tag = nothing;
    }
  }

  static get styles() {
    return [
      css`
        form {
          position: relative;
          height: 100%;
          display: flex;
          flex-direction: column;
          align-items: stretch;
          justify-content: flex-end;
        }

        #form-content {
          display: flex;
          flex-direction: column;
          flex-grow: 1;
          min-height: 400px;
        }

        #select-grid {
          display:grid;
          grid-template-columns: auto auto;
          margin: auto;
          background: rgba(255,255,255,0.3);
          z-index:999;
          align-items: baseline;
          row-gap: 5px;
          column-gap: 10px;
        }

        #container {
          position: relative;
          flex: 1 1 auto;
          overflow-y: auto;
        }

        label {
          font-weight: bolder;
          color: #718096;
        }

        #ok-cancel-buttons {
          margin-top: auto;
        }
      `,
    ];
  }

  getSelectTemplate() {
    return html`
      <h3>Create Event Sink Definition</h3>
      <div id="select-grid">
        <label for="sinkTypeList">Name</label>
        <input type="text" id="sinkDefinitionName" value="New Event Sink" required></input>
        <label for="sinkTypeList">Type</label>
        <kdsoft-checklist
          id="sinkTypeList" 
          class="text-black" 
          .model=${this.model.sinkTypeCheckListModel}
          .getItemTemplate=${item => html`${item.name}`}
          required>
        </kdsoft-checklist>
      </div>
    `;
  }

  getDefinitionTemplate() {
    return html`
      <div id="container" class="mb-4 relative">
        ${this.sinkTypeTemplateHolder.tag}
      </div>
    `;
  }

  getExportButtonTemplate() {
    if (raw(this.sinkTypeTemplateHolder.tag) == nothing) {
      return nothing;
    } else {
      return html`
          <button type="button" class="py-1 px-2" @click=${this._export} title="Export">
            <i class="fas fa-lg fa-file-export text-gray-600"></i>
          </button>
      `;
    }
  }

  getOkButtonTemplate() {
    if (raw(this.sinkTypeTemplateHolder.tag) == nothing) {
      return html`
          <button type="button" class="py-1 px-2 ml-auto" @click=${this._continue} title="Continue">
            <i class="fas fa-lg fa-chevron-right text-blue-500"></i>
          </button>
      `;
    } else {
      return html`
        <button type="button" class="py-1 px-2 ml-auto" @click=${this._apply} title="Save">
          <i class="fas fa-lg fa-check text-green-500"></i>
        </button>
      `;
    }
  }

  render() {
    const result = html`
      ${sharedStyles}
      <link rel="stylesheet" type="text/css" href=${styleLinks.checkbox} />
      <style>
        :host {
          display: block;
          height:100%;
        }
      </style>
      <form>
        <div id="form-content">
          ${raw(this.sinkTypeTemplateHolder.tag) == nothing ? this.getSelectTemplate(): this.getDefinitionTemplate()}
        </div>

        <hr class="mb-4" />
        <div id="ok-cancel-buttons" class="flex flex-wrap mt-2 bt-1">
          ${this.getExportButtonTemplate()}
          ${this.getOkButtonTemplate()}
          <button type="button" class="py-1 px-2" @click=${this._cancel} title="Cancel" autofocus>
            <i class="fas fa-lg fa-times text-red-500"></i>
          </button>
        </div>
      </form>
    `;
    return result;
  }
}

window.customElements.define('event-sink-config', EventSinkConfig);
