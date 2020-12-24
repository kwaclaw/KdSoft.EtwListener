import { html, nothing } from '../../../lib/lit-html.js';
import { LitMvvmElement, css } from '../../../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../../../lib/@nx-js/queue-util/dist/es.es6.js';
import sharedStyles from '../../../styles/kdsoft-shared-styles.js';
import styleLinks from '../../../styles/kdsoft-style-links.js';
import '../../../components/kdsoft-dropdown.js';
import '../../../components/kdsoft-checklist.js';
import '../../../components/kdsoft-expander.js';
import '../../../components/kdsoft-drop-target.js';
import '../../../components/kdsoft-tree-view.js';

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
    console.log(`${e.target.name}=${e.target.value}`);
    this.model.definition.options[e.target.name] = e.target.value;
  }

  _fieldListChange(e) {
    e.stopPropagation();
    console.log(`${e.target.name}=${e.target.value}`);
    // regular expression for splitting, removes whitespace around comma
    const sepRegex = /\s*(?:,|$)\s*/;
    this.model.definition.options[e.target.name] = (e.target.value || '').split(sepRegex);
  }

  _credentialsChange(e) {
    e.stopPropagation();
    console.log(`${e.target.name}=${e.target.value}`);
    this.model.definition.credentials[e.target.name] = e.target.value;
  }

  _validateNodeUrls(nodesElement) {
    const nodesStr = (nodesElement.value || '').trim();
    if (!nodesStr)
      return [];

    const nodes = nodesStr.split(';');
    const checkUrl = this.renderRoot.getElementById('check-url');

    const invalidUrls = [];
    for (const node of nodes) {
      checkUrl.value = node;
      if (!checkUrl.validity.valid) {
        invalidUrls.push(node);
      }
    }

    if (invalidUrls.length > 0) {
      nodesElement.setCustomValidity(`Invalid URL(s): ${invalidUrls.join(';')}`);
    } else {
      nodesElement.setCustomValidity('');
    }

    return nodes;
  }

  _nodesChanged(e) {
    e.stopPropagation();
    const nodes = this._validateNodeUrls(e.target);
    this.model.definition.options.nodes = nodes;
    e.target.reportValidity();
  }

  _nodeDeleted(e, index) {
    e.stopPropagation();
    this.model.definition.options.nodes.splice(index, 1);
  }


  // first event when model is available
  beforeFirstRender() {
    //
  }


  static get styles() {
    return [
      css`
        form {
          position: relative;
          height: 100%;
          display: flex;
          flex-direction: column;
          align-items: center;
        }

        .center {
          display: flex;
          justify-content: center;
          align-items: center;
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

        input:invalid {
          border: 2px solid red;
        }
        `,
    ];
  }

  render() {
    const opts = this.model.definition.options;
    const creds = this.model.definition.credentials;
    const nodesList = opts.nodes.join(';');

    const result = html`
      ${sharedStyles}
      <link rel="stylesheet" type="text/css" href=${styleLinks.checkbox} />
      <style>
        :host {
          display: block;
        }
      </style>
      <form>
        <input type="url" id="check-url" style="display:none"></input>
        <h3>Mongo Sink "${this.model.name}"</h3>
        <section id="options" class="center mb-5" @change=${this._optionsChange}>
          <fieldset>
            <legend>Options</legend>
            <div>
              <label for="nodes">Hosts</label>
              <input type="text" id="nodes" name="nodes" size="50" @change=${this._nodesChanged} value=${nodesList} required></input>
              <label for="index">Index</label>
              <input type="text" id="index" name="index" value=${opts.index}></input>
            </div>
          </fieldset>
        </section>
        <section id="credentials" class="center" @change=${this._credentialsChange}>
          <fieldset>
            <legend>Credentials</legend>
            <div>
              <label for="user">User</label>
              <input type="text" id="user" name="user" value=${creds.user} required></input>
              <label for="password">Password</label>
              <input type="password" id="password" name="password" value=${creds.password} required></input>
            </div>
          </fieldset>
        </section>
      </form>
    `;
    return result;
  }
}

window.customElements.define('elastic-sink-config', ElasticSinkConfig);

const tag = (model) => html`<elastic-sink-config .model=${model}></elastic-sink-config>`;

export default tag;
