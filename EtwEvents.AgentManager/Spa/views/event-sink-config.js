import { observable } from '@nx-js/observer-util/dist/es.es6.js';
import { Queue, priorities } from '@nx-js/queue-util/dist/es.es6.js';
import { LitMvvmElement, html, nothing, css } from '@kdsoft/lit-mvvm';
import checkboxStyles from '@kdsoft/lit-mvvm-components/styles/kdsoft-checkbox-styles.js';
import fontAwesomeStyles from '@kdsoft/lit-mvvm-components/styles/fontawesome/css/all-styles.js';
import tailwindStyles from '../styles/tailwind-styles.js';
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
  }

  expand() {
    const oldExpanded = this.model.expanded;
    // send event to parent to collapse other providers
    if (!oldExpanded) {
      const evt = new CustomEvent('beforeExpand', {
        // composed allows bubbling beyond shadow root
        bubbles: true, composed: true, cancelable: true, detail: { model: this.model }
      });
      this.dispatchEvent(evt);
    }
    this.model.expanded = !oldExpanded;
  }

  _expandClicked(e) {
    this.expand();
  }

  _deleteClicked(e) {
    // send event to parent to remove from list
    const evt = new CustomEvent('delete', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model: this.model }
    });
    this.dispatchEvent(evt);
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

  async _loadConfigComponent(model) {
    if (model) {
      try {
        const configFormTemplate = await loadSinkDefinitionTemplate(model);
        const configModel = await loadSinkDefinitionModel(model);
        utils.setTargetProperties(configModel, model.profile);
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
    if (!this.model.profile) return;

    const profileString = JSON.stringify(this.model.profile, null, 2);
    const profileURL = `data:text/plain,${profileString}`;

    const a = document.createElement('a');
    try {
      a.style.display = 'none';
      a.href = profileURL;
      a.download = `${this.model.profile.name}.json`;
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
    this._loadConfigComponent(this.model);
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

        #processing-vars {
          display: grid;
          grid-template-columns: auto auto;
          background: rgba(255,255,255,0.3);
          row-gap: 5px;
          column-gap: 10px;
          margin-bottom: 10px;
        }
      `,
    ];
  }

  render() {
    const profile = this.model.profile;
    const expanded = this.model.expanded || false;
    const borderColor = expanded ? 'border-indigo-500' : 'border-transparent';
    const timesClasses = 'text-gray-600 fas fa-lg fa-times';
    const chevronClasses = expanded
      ? 'text-indigo-500 fas fa-lg  fa-chevron-circle-up'
      : 'text-gray-600 fas fa-lg fa-chevron-circle-down';
    const errorClasses = this.model.error ? 'border-red-500 focus:outline-none focus:border-red-700' : '';

    const result = html`
      <div class="border-l-2 ${borderColor}">
        <header class="flex items-center justify-start pl-1 cursor-pointer select-none relative ${errorClasses}">
          <span>${profile.name} - ${profile.sinkType}</span>
          <span class="${timesClasses} ml-auto mr-2" @click=${this._deleteClicked}></span>
          <span class="${chevronClasses}" @click=${this._expandClicked}></span>
        </header>

        <form class="mt-2 relative" ?hidden=${!expanded}>
          <div id="form-header">
            <pre ?hidden=${!this.model.error}><textarea 
              class="my-2 w-full border-2 border-red-500 focus:outline-none focus:border-red-700"
            >${this.model.error}</textarea></pre>

            <div id="processing-vars" @change=${this._profileChange}>
              <label for="batchSize">Batch Size</label>
              <input type="number" id="batchSize" name="batchSize" .value=${profile.batchSize} />
              <label for="maxWriteDelayMSecs">Max Write Delay (msecs)</label>
              <input type="number" id="maxWriteDelayMSecs" name="maxWriteDelayMSecs" value=${profile.maxWriteDelayMSecs} />
              <label for="persistentChannel">Persistent Buffer</label>
              <input type="checkbox" id="persistentChannel" name="persistentChannel" value=${profile.persistentChannel} />
            </div>
          </div>
          <div id="form-content">
            ${this.sinkTypeTemplateHolder.tag}
          </div>
        </form>
      </div>
    `;
    return result;
  }
}

window.customElements.define('event-sink-config', EventSinkConfig);
