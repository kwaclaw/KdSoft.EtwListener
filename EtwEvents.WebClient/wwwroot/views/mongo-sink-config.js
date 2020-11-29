import { html, nothing } from '../lib/lit-html.js';
import { LitMvvmElement, css } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util/dist/es.es6.js';
import sharedStyles from '../styles/kdsoft-shared-styles.js';
import styleLinks from '../styles/kdsoft-style-links.js';
import '../components/kdsoft-dropdown.js';
import '../components/kdsoft-checklist.js';
import '../components/kdsoft-expander.js';
import '../components/kdsoft-drop-target.js';
import '../components/kdsoft-tree-view.js';

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

  _hostChanged(e) {
    e.stopPropagation();
    console.log(`${e.target.name}=${e.target.value}`);
    // if (e.target.validity.valid && e.target.value && e.target.value.trim()) {
    //   this.model.options.hosts.push(e.target.value);
    //   e.target.value = '';
    // }
    let isValid = true;
    if (e.target.value && e.target.value.trim()) {
      const hosts = e.target.value.split(';');
      const checkUrl = this.renderRoot.getElementById('check-url');
      for (const host of hosts) {
        checkUrl.value = host;
        isValid = isValid && checkUrl.validity.valid;
      }
      this.model.definition.options.hosts = hosts;
    }
    else {
      this.model.definition.options.hosts = [];
    }
    e.target.setCustomValidity(isValid ? '' : 'Not an URL');
    e.target.reportValidity();
  }

  _hostDeleted(e, index) {
    e.stopPropagation();
    this.model.definition.options.hosts.splice(index, 1);
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
          grid-template-columns: 12em 1fr;
          align-items: baseline;
          grid-gap: 5px;
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
    const hostsList = opts.hosts.join(';');
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
              <label for="hosts">Hosts</label>
              <input type="text" id="hosts" name="hosts" size="50" @change=${this._hostChanged} value=${hostsList}></input>
              <label for="replicaset">Replica Set</label>
              <input type="text" id="replicaset" name="replicaset" value=${opts.replicaSet}></input>
              <label for="database">Database</label>
              <input type="text" id="database" name="database" value=${opts.database}></input>
              <label for="collection">Collection</label>
              <input type="text" id="collection" name="collection" value=${opts.database}></input>
            </div>
          </fieldset>
        </section>
        <section id="credentials" class="center" @change=${this._credentialsChange}>
          <fieldset>
            <legend>Credentials</legend>
            <div>
              <label for="cred-database">Database</label>
              <input type="text" id="cred-database" name="cred-database" value=${creds.database}></input>
              <label for="user">User</label>
              <input type="text" id="user" name="user" value=${creds.user}></input>
              <label for="password">Password</label>
              <input type="password" id="password" name="password">${creds.password}</input>
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
