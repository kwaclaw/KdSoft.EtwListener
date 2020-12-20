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

class MongoSinkConfig extends LitMvvmElement {
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

  _credentialsChange(e) {
    e.stopPropagation();
    console.log(`${e.target.name}=${e.target.value}`);
    this.model.definition.credentials[e.target.name] = e.target.value;
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
              <label for="origin">Origin</label>
              <input type="url" id="origin" name="origin" size="50" value=${opts.origin} required></input>
              <label for="replicaset">Replica Set</label>
              <input type="text" id="replicaset" name="replicaset" value=${opts.replicaset}></input>
              <label for="database">Database</label>
              <input type="text" id="database" name="database" value=${opts.database} required></input>
              <label for="collection">Collection</label>
              <input type="text" id="collection" name="collection" value=${opts.collection} required></input>
            </div>
          </fieldset>
        </section>
        <section id="credentials" class="center" @change=${this._credentialsChange}>
          <fieldset>
            <legend>Credentials</legend>
            <div>
              <label for="cred-database">Database</label>
              <input type="text" id="cred-database" name="database" value=${creds.database} required></input>
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

window.customElements.define('mongo-sink-config', MongoSinkConfig);

const tag = (model) => html`<mongo-sink-config .model=${model}></mongo-sink-config>`;

export default tag;
