import { LitMvvmElement, html, css } from '@kdsoft/lit-mvvm';
import { Queue, priorities } from '@nx-js/queue-util/dist/es.es6.js';
import tailwindStyles from '@kdsoft/lit-mvvm-components/styles/tailwind-styles.js';
import * as utils from '../../js/utils.js';

class ElasticSinkConfig extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
  }

  isValid() {
    return this.renderRoot.querySelector('form').reportValidity();
  }

  _optionsChange(e) {
    e.stopPropagation();
    this.model.options[e.target.name] = utils.getFieldValue(e.target);
  }

  _setValidatedCredentials() {
    const credElement = this.renderRoot.getElementById('credentials');
    const user = utils.getFieldValue(this.renderRoot.getElementById('user'));
    const password = utils.getFieldValue(this.renderRoot.getElementById('password'));
    const apiKey = utils.getFieldValue(this.renderRoot.getElementById('apiKey'));
    const subjectCN = utils.getFieldValue(this.renderRoot.getElementById('subjectCN'));
    if (!!user && !!password) {
      credElement.setCustomValidity('');
    } else if (!!apiKey || !!subjectCN) {
      credElement.setCustomValidity('');
    } else {
      credElement.setCustomValidity('Require credentials: user and password, or apiKey or subjectCN.');
      return false;
    }
    this.model.user = user;
    this.model.password = password;
    this.model.apiKey = apiKey;
    this.model.subjectCN = subjectCN;
    return true;
  }

  _credentialsChange(e) {
    e.stopPropagation();
    this._setValidatedCredentials();
    e.target.reportValidity();
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

        section {
          min-width: 75%;
        }

        section fieldset {
          padding: 5px;
          border-width: 1px;
        }

        section fieldset > div {
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
        <section id="options" class="mb-5" @change=${this._optionsChange}>
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
        </section>
        <section id="credentials" @change=${this._credentialsChange}>
          <fieldset>
            <legend>Credentials</legend>
            <div>
              <label for="user">User</label>
              <input type="text" id="user" name="user" .value=${creds.user}></input>
              <label for="password">Password</label>
              <input type="password" id="password" name="password" .value=${creds.password}></input>
              <label for="apiKey">API Key</label>
              <input type="text" id="apiKey" name="apiKey" .value=${creds.apiKey}></input>
              <label for="subjectCN">Certificate Subject CN</label>
              <input type="text" id="subjectCN" name="subjectCN" .value=${creds.subjectCN}></input>
            </div>
          </fieldset>
        </section>
      </form>
    `;
    return result;
  }
}

window.customElements.define('elastic-sink-config', ElasticSinkConfig);

const tag = model => html`<elastic-sink-config .model=${model}></elastic-sink-config>`;

export default tag;
