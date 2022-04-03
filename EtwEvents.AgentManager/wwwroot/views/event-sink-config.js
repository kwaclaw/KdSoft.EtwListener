import { observe, observable } from '@nx-js/observer-util';
import { Queue, priorities } from '@nx-js/queue-util';
import { LitMvvmElement, html, nothing, css } from '@kdsoft/lit-mvvm';
import checkboxStyles from '@kdsoft/lit-mvvm-components/styles/kdsoft-checkbox-styles.js';
import fontAwesomeStyles from '@kdsoft/lit-mvvm-components/styles/fontawesome/css/all-styles.js';
import tailwindStyles from '../styles/tailwind-styles.js';
import * as utils from '../js/utils.js';

function sinkConfigForm(sinkType) {
  switch (sinkType) {
    case 'ElasticSink':
      return 'elastic-sink-config';
    case 'gRPCSink':
      return 'grpc-sink-config';
    case 'MongoSink':
      return 'mongo-sink-config';
    case 'RollingFileSink':
      return 'rolling-file-sink-config';
    case 'SeqSink':
      return 'seq-sink-config';
    default:
      throw new Error(`No configuration form for '${sinkType}'.`);
  }
}

function sinkConfigModel(sinkType) {
  switch (sinkType) {
    case 'ElasticSink':
      return 'elastic-sink-config-model';
    case 'gRPCSink':
      return 'grpc-sink-config-model';
    case 'MongoSink':
      return 'mongo-sink-config-model';
    case 'RollingFileSink':
      return 'rolling-file-sink-config-model';
    case 'SeqSink':
      return 'seq-sink-config-model';
    default:
      throw new Error(`No configuration model for '${sinkType}'.`);
  }
}

async function loadSinkDefinitionTemplate(sinkType) {
  // Vite can only analyze the dynamic import if we provide a file extension
  const elementModule = await import(`../eventSinks/${sinkType}/${sinkConfigForm(sinkType)}.js`);
  const configElement = elementModule.default;
  return configElement;
}

async function loadSinkDefinitionModel(sinkType) {
  // Vite can only analyze the dynamic import if we provide a file extension
  const modelModule = await import(`../eventSinks/${sinkType}/${sinkConfigModel(sinkType)}.js`);
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

  _fieldChange(e) {
    e.stopPropagation();
    this.model.profile[e.target.name] = utils.getFieldValue(e.target);
  }

  isValid() {
    return this.renderRoot.querySelector('form').reportValidity()
      && this.renderRoot.querySelector('#form-content > *')?.isValid();
  }

  async _loadConfigComponent(model) {
    if (model) {
      try {
        const configFormTemplate = await loadSinkDefinitionTemplate(model.profile.sinkType);
        this.sinkConfigModel = await loadSinkDefinitionModel(model.profile.sinkType);
        // update the sinkConfigModel's credentials and options separately from profile,
        // as otherwise we would just replace the options and credentials properties entirely
        utils.setTargetProperties(this.sinkConfigModel.credentials, model.profile.credentials);
        utils.setTargetProperties(this.sinkConfigModel.options, model.profile.options);
        this.sinkTypeTemplateHolder.tag = configFormTemplate(this.sinkConfigModel);
      } catch (error) {
        // for "nothing" to work we need to render raw(this.sinkTypeTemplateHolder.value)
        this.sinkTypeTemplateHolder.tag = nothing;
        window.etwApp.defaultHandleError(error);
      }
    } else {
      this.sinkTypeTemplateHolder.tag = nothing;
    }
  }

  /* eslint-disable indent, no-else-return */

  shouldRender() {
    return !!this.model;
  }

  async beforeFirstRender() {
    await this._loadConfigComponent(this.model);
    this.configObserver = observe(() => {
      this.model.profile.credentials = this.sinkConfigModel.credentials || {};
      this.model.profile.options = this.sinkConfigModel.options || {};
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
          row-gap: 5px;
          column-gap: 10px;
          margin-bottom: 15px;
          padding-left: 5px;
          background: rgba(255,255,255,0.3);
        }

        #form-header>pre {
          grid-column: 1/-1;
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

        input:invalid {
          border: 2px solid red;
        }

        #common-fields {
          display: grid;
          grid-template-columns: auto auto;
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
    const titleClasses = this.model.error ? 'text-red-600' : (expanded ? 'text-indigo-500' : '');

    const result = html`
      <div class="border-l-2 ${borderColor}">
        <header class="flex items-center justify-start pl-1 py-2 cursor-pointer select-none relative ${errorClasses}">
          <span class="${titleClasses}">${profile.name} - ${profile.sinkType}</span>
          <span class="${timesClasses} ml-auto mr-2" @click=${this._deleteClicked}></span>
          <span class="${chevronClasses}" @click=${this._expandClicked}></span>
        </header>

        <form class="relative" ?hidden=${!expanded}>
          <div id="form-header">
            <pre ?hidden=${!this.model.error}><textarea 
              class="my-2 w-full border-2 border-red-500 focus:outline-none focus:border-red-700"
            >${this.model.error}</textarea></pre>

            <div id="common-fields" @change=${this._fieldChange}>
              <label for="batchSize">Batch Size</label>
              <input type="number" id="batchSize" name="batchSize" .value=${profile.batchSize} min="1" />
              <label for="maxWriteDelayMSecs">Max Write Delay (msecs)</label>
              <input type="number" id="maxWriteDelayMSecs" name="maxWriteDelayMSecs" .value=${profile.maxWriteDelayMSecs} min="0" />
              <label for="persistentChannel">Persistent Buffer</label>
              <input type="checkbox" id="persistentChannel" name="persistentChannel" .checked=${profile.persistentChannel} />
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
