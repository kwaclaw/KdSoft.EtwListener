import { observable, observe, unobserve, raw } from '@nx-js/observer-util/dist/es.es6.js';
import { Queue, priorities } from '@nx-js/queue-util/dist/es.es6.js';
import { LitMvvmElement, html, nothing, css } from '@kdsoft/lit-mvvm';
import tailwindStyles from '@kdsoft/lit-mvvm-components/styles/tailwind-styles.js';
import checkboxStyles from '@kdsoft/lit-mvvm-components/styles/kdsoft-checkbox-styles.js';
import fontAwesomeStyles from '@kdsoft/lit-mvvm-components/styles/fontawesome/css/all-styles.js';
import {
  KdSoftDropdownModel,
  KdSoftDropdownChecklistConnector,
} from '@kdsoft/lit-mvvm-components';
import EventSinkProfile from '../js/eventSinkProfile.js';
import * as utils from '../js/utils.js';

/* Event Sink UI
   - should reflect configuration stored in PushAgent's eventSink.json, passed as EventSinkState
   - should allow editing details
   - should allow to change the event sink type - which will trigger a different UI
     if another type is selected - so the type should be in the header with a dropdown
   - changing the dropdown selection will change the UI in the body
*/

async function loadSinkDefinitionTemplate(sinkInfo) {
  const elementModule = await import(/* @vite-ignore */sinkInfo.configViewUrl);
  const configElement = elementModule.default;
  return configElement;
}

async function loadSinkDefinitionModel(sinkInfo) {
  const modelModule = await import(/* @vite-ignore */sinkInfo.configModelUrl);
  const ModelClass = modelModule.default;
  return new ModelClass();
}

class EventSinkConfig extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
    // for "nothing" to work we need to render raw(this.sinkTypeTemplateHolder.value)
    this.sinkTypeTemplateHolder = observable({ tag: nothing });
    this.dropDownModel = observable(new KdSoftDropdownModel());
    this.checklistConnector = new KdSoftDropdownChecklistConnector(
      () => this.renderRoot.getElementById('sinktype-ddown'),
      () => this.renderRoot.getElementById('sinktype-list'),
      listModel => {
        const item = listModel.firstSelectedEntry?.item;
        if (item) return `${item.sinkType} (${item.version})`;
        return '';
      }
    );
  }

  _profileChange(e) {
    e.stopPropagation();
    this.model.sinkProfile[e.target.name] = utils.getFieldValue(e.target);
  }

  _cancel() {
    const container = this.renderRoot.getElementById('form-content');
    const configElement = container.children[0];

    const evt = new CustomEvent('kdsoft-done', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model: configElement?.model, canceled: true }
    });
    this.dispatchEvent(evt);
  }

  _apply(e) {
    const container = this.renderRoot.getElementById('form-content');
    const configElement = container.children[0];
    if (!configElement || !configElement.isValid()) return;

    const nameField = this.renderRoot.getElementById('sinkProfileName');
    const selectedSinkInfoEntry = this.model.sinkInfoCheckListModel.firstSelectedEntry;
    const selectedSinkInfo = selectedSinkInfoEntry?.item;
    const sinkProfile = new EventSinkProfile(nameField.value, selectedSinkInfo.sinkType, selectedSinkInfo.version);
    Object.assign(sinkProfile, configElement.model);

    this.model.sinkProfile = sinkProfile;

    const evt = new CustomEvent('kdsoft-done', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model: this.model, canceled: false }
    });
    this.dispatchEvent(evt);
  }

  _isValid() {
    return this.renderRoot.querySelector('#form-content form').reportValidity();
  }

  async _loadConfigComponent(sinkInfo, sinkProfile) {
    if (sinkInfo) {
      try {
        const configFormTemplate = await loadSinkDefinitionTemplate(sinkInfo);
        const configModel = await loadSinkDefinitionModel(sinkInfo);

        if (sinkProfile && sinkProfile.sinkType === sinkInfo.sinkType && sinkProfile.version === sinkInfo.version) {
          utils.setTargetProperties(configModel, sinkProfile);
        } else {
          sinkProfile = new EventSinkProfile('Default', sinkInfo.sinkType, sinkInfo.version);
          utils.setTargetProperties(sinkProfile, configModel);
          this.model.sinkProfile = sinkProfile;
        }

        this.sinkTypeTemplateHolder.tag = configFormTemplate(configModel);
      } catch (error) {
        // for "nothing" to work we need to render raw(this.sinkTypeTemplateHolder.value)
        this.sinkTypeTemplateHolder.tag = nothing;
        window.etwApp.defaultHandleError(error);
      }
    } else {
      this.sinkTypeTemplateHolder.tag = nothing;
    }
  }

  _export() {
    if (!this.model.sinkProfile) return;

    const profileString = JSON.stringify(this.model.sinkProfile, null, 2);
    const profileURL = `data:text/plain,${profileString}`;

    const a = document.createElement('a');
    try {
      a.style.display = 'none';
      a.href = profileURL;
      a.download = `${this.model.sinkProfile.name}.json`;
      document.body.appendChild(a);
      a.click();
    } finally {
      document.body.removeChild(a);
    }
  }

  /* eslint-disable indent, no-else-return */

  shouldRender() {
    return !!this.model;
  }

  firstRendered() {
    if (this._eventSinkObserver) {
      unobserve(this._eventSinkObserver);
    }
    this._eventSinkObserver = observe(() => {
      // need to read observed properties synchronously!
      const selectedSinkInfoEntry = this.model.sinkInfoCheckListModel.firstSelectedEntry;
      const sinkProfile = this.model.sinkProfile;
      this._loadConfigComponent(selectedSinkInfoEntry?.item, sinkProfile);
    });
  }

  static get styles() {
    return [
      tailwindStyles,
      checkboxStyles,
      fontAwesomeStyles,
      //appStyles,
      css`
        :host {
          display: block;
        }

        form {
          position: relative;
          display: flex;
          flex-direction: column;
          align-items: stretch;
          justify-content: flex-end;
        }

        #form-header {
          display: grid;
          grid-template-columns: auto auto;
          background: rgba(255,255,255,0.3);
          row-gap: 5px;
          column-gap: 10px;
          margin-bottom: 15px;
        }

        #form-content {
          position: relative;
          display: flex;
          flex-direction: column;
          flex-grow: 1;
          overflow-y: auto;
          min-height: 200px;
        }

        label {
          font-weight: bolder;
          color: #718096;
        }

        input, textarea {
          border-width: 1px;
        }

        #ok-cancel-buttons {
          margin-top: auto;
        }
      `,
    ];
  }

  render() {
    const result = html`
      <form>
        <div id="form-header">
          <label for="sinktype-ddown">Event Sink Type</label>
          <kdsoft-dropdown id="sinktype-ddown" class="py-0"
            .model=${this.dropDownModel} .connector=${this.checklistConnector}>
            <kdsoft-checklist
              id="sinktype-list" 
              class="text-black" 
              .model=${this.model.sinkInfoCheckListModel}
              .getItemTemplate=${item => html`<div class="flex w-full"><span class="mr-1">${item.sinkType}</span><span class="ml-auto">(${item.version})</span></div>`}
              required>
            </kdsoft-checklist>
          </kdsoft-dropdown>
          <label for="sinktype-ddown">Profile Name</label>
          <input id="sinkProfileName" name="name" type="text" @change=${this._profileChange} .value=${this.model.sinkProfile?.name} />
        </div>

        <div id="form-content">
          ${this.sinkTypeTemplateHolder.tag}
        </div>

        <hr class="mb-4" />
        <div id="ok-cancel-buttons" class="flex flex-wrap mt-2 bt-1">
          <button type="button" class="py-1 px-2" @click=${this._export} title="Export">
            <i class="fas fa-lg fa-file-export text-gray-600"></i>
          </button>
          <button type="button" class="py-1 px-2 ml-auto" @click=${this._apply} title="Apply">
            <i class="fas fa-lg fa-check text-green-500"></i>
          </button>
          <button type="button" class="py-1 px-2" @click=${this._cancel} title="Reset to Current" autofocus>
            <i class="fas fa-lg fa-times text-red-500"></i>
          </button>
        </div>
      </form>
    `;
    return result;
  }

  rendered() {
    this.checklistConnector.reconnectDropdownSlot();
  }
}

window.customElements.define('event-sink-config', EventSinkConfig);
