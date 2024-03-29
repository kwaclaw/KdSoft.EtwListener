import { observable, observe } from '@nx-js/observer-util';
import { LitMvvmElement, html, css } from '@kdsoft/lit-mvvm';
import {
  KdsListModel,
} from '@kdsoft/lit-mvvm-components';
import tailwindStyles from '../../styles/tailwind-styles.js';
import checkboxStyles from '../../styles/kds-checkbox-styles.js';
import fontAwesomeStyles from '../../styles/fontawesome/css/all-styles.js';
import MongoSinkConfigModel from './mongo-sink-config-model.js';
import '../../components/etw-checklist.js';
import '../../components/valid-section.js';
import * as utils from '../../js/utils.js';

class MongoSinkConfig extends LitMvvmElement {
  constructor() {
    super();
    this.evtFieldChecklistModel = observable(new KdsListModel(
      MongoSinkConfigModel.eventFields,
      [],
      true,
      item => item.id
    ));
  }

  isValid() {
    const validatedSection = this.renderRoot.getElementById('credentials');
    this._setValidatedCredentials(validatedSection);
    return this.renderRoot.querySelector('form').reportValidity();
  }

  _optionsChange(e) {
    e.stopPropagation();
    this.model.options[e.target.name] = utils.getFieldValue(e.target);
  }

  _fieldListChange(e) {
    e.stopPropagation();
    // regular expression for splitting, removes whitespace around comma
    const sepRegex = /\s*(?:,|$)\s*/;
    this.model.options[e.target.name] = (e.target.value || '').split(sepRegex);
  }

  _setValidatedCredentials(validatedElement) {
    const certCN = utils.getFieldValue(this.renderRoot.getElementById('certCN'));
    const user = utils.getFieldValue(this.renderRoot.getElementById('user'));
    const pwd = utils.getFieldValue(this.renderRoot.getElementById('password'));
    if (certCN || (user && pwd)) {
      validatedElement.setCustomValidity('');
      this.model.credentials.certificateCommonName = certCN;
      this.model.credentials.user = user;
      this.model.credentials.password = pwd;
      return true;
    }
    // change validity on first empty control, we can't really use a hidden/invisible/zero-size control
    // as the browser will not show the message on a hidden or invisible or zero-size control
    const msg = 'At least one of certificate or user/password information must be filled in.';
    validatedElement.setCustomValidity(msg);
    return false;
  }

  _credentialsChange(e) {
    e.stopPropagation();
    if (e.target.name === 'database') {
      this.model.credentials[e.target.name] = utils.getFieldValue(e.target);
    } else {
      const validatedSection = this.renderRoot.getElementById('credentials');
      this._setValidatedCredentials(validatedSection);
      validatedSection.reportValidity();
    }
  }

  // first event when model is available
  beforeFirstRender() {
    // save state temporarily because this._eventFieldsObserver will reset it on unselectAll()
    const eventFilterFields = this.model.options.eventFilterFields;
    this.evtFieldChecklistModel.unselectAll();
    this.evtFieldChecklistModel.selectIds(eventFilterFields, true);
  }

  static get styles() {
    return [
      tailwindStyles,
      fontAwesomeStyles,
      checkboxStyles,
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
          min-width: 100%;
        }

        valid-section fieldset {
          padding: 5px;
          border-width: 1px;
        }

        #options fieldset > div {
          display:grid;
          grid-template-columns: auto auto;
          /* grid-template-rows: repeat(8, auto); */
          align-items: baseline;
          row-gap: 5px;
          column-gap: 10px;
        }

        #options fieldset > div > div {
          display:grid;
        }

        #credentials {
          width:100%;
        }

        #credentials fieldset > div {
          display:grid;
          /* grid-template-columns: auto auto; */
          align-items: baseline;
          row-gap: 5px;
          column-gap: 10px;
        }

        input:invalid {
          border: 2px solid red;
        }

        /*
        valid-section fieldset > div > label {
          grid-column: 1;
        }

        valid-section fieldset > div > input {
          grid-column: 2;
        }

        label[for="evtFieldList"] {
          grid-column: 3;
          grid-row: 1;
        }

        #evtFieldList {
          grid-column: 3 / 3;
          grid-row: 2 / -1;
          display: flex;
          position: relative;
          height: 100%;
        }

        #evtFieldList etw-checklist {
          position: absolute;
          left:0;
          top:0;
          right:0;
          bottom:0;
          background-color: lightgray;
          --max-scroll-height: 100%;
        }
        */

        .pad {
          height: 3ex;
        }
        `,
    ];
  }

  render() {
    const opts = this.model.options;
    const creds = this.model.credentials;
    const payloadFieldsList = opts.payloadFilterFields?.join(', ') || [];

    const result = html`
      <form>
        <valid-section id="options" class="mb-5" @change=${this._optionsChange}>
          <fieldset>
            <legend>Options</legend>
            <div>
              <div>
                <label for="origin">Origin</label>
                <input type="url" id="origin" name="origin" size="50" .value=${opts.origin} required></input>
                <label for="replicaset">Replica Set</label>
                <input type="text" id="replicaset" name="replicaset" .value=${opts.replicaset}></input>
                <label for="database">Database</label>
                <input type="text" id="database" name="database" .value=${opts.database} required></input>
                <label for="collection">Collection</label>
                <input type="text" id="collection" name="collection" .value=${opts.collection} required></input>
                <label for="payloadFilterFields">Payload Filter Fields</label>
                <input type="text"
                  id="payloadFilterFields" name="payloadFilterFields"
                  .value=${payloadFieldsList} @change=${this._fieldListChange}
                  placeholder="Comma delimited list"></input>
              </div>
              <div>
                <label for="evtFieldList">Event Filter Fields</label>
                <div id="evtFieldList">
                  <etw-checklist class="text-black"
                    .model=${this.evtFieldChecklistModel}
                    .itemTemplate=${item => html`${item.id}`}
                    checkboxes>
                  </etw-checklist>
                </div>
              </div>
            </div>
          </fieldset>
        </valid-section>
        <valid-section id="credentials" @change=${this._credentialsChange}>
          <fieldset>
            <legend>Credentials</legend>
            <div>
              <label for="cred-database">Database</label>
              <input type="text" id="cred-database" name="database" .value=${creds.database} required></input>
              <label for="certCN">Certificate Common Name</label>
              <input type="text" id="certCN" name="certificateCommonName" .value=${creds.certificateCommonName}></input>
              <label for="user">User</label>
              <input type="text" id="user" name="user" .value=${creds.user}></input>
              <label for="password">Password</label>
              <input type="password" id="password" name="password" .value=${creds.password}></input>
            </div>
          </fieldset>
        </valid-section>
      </form>
    `;
    return result;
  }

  firstRendered() {
    if (!this._eventFieldsObserver) {
      this._eventFieldsObserver = observe(() => {
        const selectedIds = Array.from(this.evtFieldChecklistModel.selectedEntries).map(entry => entry.item.id);
        // since we are already reacting to the selection change, let's update the underlying model
        this.model.options.eventFilterFields = selectedIds;
      });
    }
  }
}

window.customElements.define('mongo-sink-config', MongoSinkConfig);

const tag = model => html`<mongo-sink-config .model=${model}></mongo-sink-config>`;

export default tag;
