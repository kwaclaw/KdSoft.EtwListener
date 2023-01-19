import { LitMvvmElement, html, css, BatchScheduler } from '@kdsoft/lit-mvvm';
import { Queue, priorities } from '@nx-js/queue-util';
import tailwindStyles from '../../styles/tailwind-styles.js';
import '../../components/valid-section.js';
import * as utils from '../../js/utils.js';

class ElasticSinkConfig extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = window.renderScheduler;
  }

  isValid() {
    const validatedElement = this.renderRoot.getElementById('credentials');
    this._setValidatedCredentials(validatedElement);
    return this.renderRoot.querySelector('form').reportValidity();
  }

  _optionsChange(e) {
    e.stopPropagation();
    this.model.options[e.target.name] = utils.getFieldValue(e.target);
  }

  _setValidatedCredentials(validatedElement) {
    const user = utils.getFieldValue(this.renderRoot.getElementById('user'));
    const password = utils.getFieldValue(this.renderRoot.getElementById('password'));
    const apiKeyId = utils.getFieldValue(this.renderRoot.getElementById('apiKeyId'));
    const apiKey = utils.getFieldValue(this.renderRoot.getElementById('apiKey'));
    const subjectCN = utils.getFieldValue(this.renderRoot.getElementById('subjectCN'));
    if (!!user && !!password) {
      validatedElement.setCustomValidity('');
    } else if (!!apiKey) {
      validatedElement.setCustomValidity('');
    } else if (!!subjectCN) {
      validatedElement.setCustomValidity('');
    } else {
      validatedElement.setCustomValidity('Require credentials: user and password, or apiKeyId (optional) and apiKey, or subjectCN.');
      return false;
    }
    this.model.credentials.user = user;
    this.model.credentials.password = password;
    this.model.credentials.apiKeyId = apiKeyId;
    this.model.credentials.apiKey = apiKey;
    this.model.credentials.subjectCN = subjectCN;
    return true;
  }

  _credentialsChange(e) {
    e.stopPropagation();
    const validatedElement = this.renderRoot.getElementById('credentials');
    this._setValidatedCredentials(validatedElement);
    validatedElement.reportValidity();
  }

  _validateNodeUrls(nodesElement) {
    const nodesStr = (nodesElement.value || '').trim();
    if (!nodesStr) return [];

    const nodes = nodesStr.split('\n');
    const checkUrl = this.renderRoot.getElementById('check-url');

    const invalidUrls = [];
    for (const node of nodes) {
      checkUrl.value = node;
      if (!checkUrl.validity.valid) {
        invalidUrls.push(node);
      }
    }

    if (invalidUrls.length > 0) {
      nodesElement.setCustomValidity(`Invalid URL(s):\n- ${invalidUrls.join('\n- ')}`);
    } else {
      nodesElement.setCustomValidity('');
    }

    return nodes;
  }

  _nodesChanged(e) {
    e.stopPropagation();
    const nodes = this._validateNodeUrls(e.target);
    this.model.options.nodes = nodes;
    e.target.reportValidity();
  }

  _nodeDeleted(e, index) {
    e.stopPropagation();
    this.model.options.nodes.splice(index, 1);
  }

  // first event when model is available
  beforeFirstRender() {
    //
  }

  static get styles() {
    return [
      tailwindStyles,
      css`
        :host {
          display: block;
        }

        form {
          position: relative;
          height: 100%;
          display: flex;
          flex-direction: column;
          align-items: flex-start;
        }

        label {
          font-weight: bolder;
          color: #718096;
        }

        valid-section {
          min-width: 75%;
        }

        valid-section fieldset {
          padding: 5px;
          border-width: 1px;
        }

        valid-section fieldset > div {
          display:grid;
          grid-template-columns: auto auto;
          align-items: baseline;
          row-gap: 5px;
          column-gap: 10px;
        }

        input, textarea {
          border-width: 1px;
        }

        input:invalid, textarea:invalid {
          border: 2px solid red;
        }

        #nodes {
          resize: both;
        }
        `,
    ];
  }

  render() {
    const opts = this.model.options;
    const creds = this.model.credentials;
    const nodesList = opts.nodes?.join('\n') || [];

    const result = html`
      <form>
        <input type="url" id="check-url" name="url" style="display:none" />
        <valid-section id="options" class="mb-5" @change=${this._optionsChange}>
          <fieldset>
            <legend>Options</legend>
            <div>
              <label for="nodes">Hosts</label>
              <textarea id="nodes" name="nodes"
                cols="42" rows="3" wrap="hard"
                @change=${this._nodesChanged}
                placeholder="Enter one or more URLs, each on its own line" required>${nodesList}</textarea>
              <label for="index">Index Format</label>
              <input type="text" id="indexFormat" name="indexFormat" .value=${opts.indexFormat} />
            </div>
          </fieldset>
        </valid-section>
        <valid-section id="credentials" @change=${this._credentialsChange}>
          <fieldset>
            <legend>Credentials</legend>
            <div>
              <label for="user">User</label>
              <input type="text" id="user" name="user" .value=${creds.user}></input>
              <label for="password">Password</label>
              <input type="password" id="password" name="password" .value=${creds.password}></input>
              <label for="apiKeyId">API Key Id  (non-base64 keys)</label>
              <input type="text" id="apiKeyId" name="apiKeyId" .value=${creds.apiKeyId}></input>
              <label for="apiKey">API Key</label>
              <input type="password" id="apiKey" name="apiKey" .value=${creds.apiKey}></input>
              <label for="subjectCN">Certificate Subject CN</label>
              <input type="text" id="subjectCN" name="subjectCN" .value=${creds.subjectCN}></input>
            </div>
          </fieldset>
        </valid-section>
      </form>
    `;
    return result;
  }
}

window.customElements.define('elastic-sink-config', ElasticSinkConfig);

const tag = model => html`<elastic-sink-config .model=${model}></elastic-sink-config>`;

export default tag;
