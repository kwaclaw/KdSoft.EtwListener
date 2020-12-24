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
import KdSoftDropdownModel from '../../../components/kdsoft-dropdown-model.js';
import KdSoftChecklistModel from '../../../components/kdsoft-checklist-model.js';
import KdSoftDropdownChecklistConnector from '../../../components/kdsoft-dropdown-checklist-connector.js';
import MongoSinkConfigModel from './mongo-sink-config-model.js';

class MongoSinkConfig extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
    this.evtFieldsDropDownModel = new KdSoftDropdownModel();
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

  // first event when model is available
  beforeFirstRender() {
    if (!this.evtFieldChecklistModel) {
      this.evtFieldChecklistModel = new KdSoftChecklistModel(
        MongoSinkConfigModel.eventFields,
        [],
        true,
        item => item.id
      );
      // we select by item ids as we have these readily available
      this.evtFieldChecklistModel.selectIds(this.model.definition.options.eventFilterFields || [], true);
    }

    this.evtFieldsChecklistConnector = new KdSoftDropdownChecklistConnector(
      () => this.renderRoot.getElementById('evtFields'),
      () => this.renderRoot.getElementById('evtFieldList'),
      (chkListModel) => {
        const selectedIds = Array.from(chkListModel.selectedEntries).map(entry => entry.item.id);
        // since we are already reacting to the selection change, let's update the underlying model
        this.model.definition.options.eventFilterFields = selectedIds;
        return selectedIds.join(', ');
      }
    );
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
    const payloadFieldsList = opts.payloadFilterFields.join(', ');

    const result = html`
      ${sharedStyles}
      <link rel="stylesheet" type="text/css" href=${styleLinks.checkbox} />
      <style>
        :host {
          display: block;
        }
      </style>
      <form>
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
              <label for="eventFilterFields">Event Filter Fields</label>
              <kdsoft-dropdown id="evtFields" class="py-0" .model=${this.evtFieldsDropDownModel} .connector=${this.evtFieldsChecklistConnector}>
                <kdsoft-checklist
                  id="evtFieldList"
                  class="text-black"
                  .model=${this.evtFieldChecklistModel}
                  .getItemTemplate=${item => html`${item.id}`}
                  show-checkboxes>
                </kdsoft-checklist>
              </kdsoft-dropdown>
              <label for="payloadFilterFields">Payload Filter Fields</label>
              <input type="text" id="payloadFilterFields" name="payloadFilterFields" value=${payloadFieldsList} @change=${this._fieldListChange}></input>
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
