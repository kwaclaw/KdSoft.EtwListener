﻿import { LitMvvmElement, html, css } from '@kdsoft/lit-mvvm';
import { Queue, priorities } from '@nx-js/queue-util/dist/es.es6.js';
import tailwindStyles from '@kdsoft/lit-mvvm-components/styles/tailwind-styles.js';
import checkboxStyles from '@kdsoft/lit-mvvm-components/styles/kdsoft-checkbox-styles.js';
import fontAwesomeStyles from '@kdsoft/lit-mvvm-components/styles/fontawesome/css/all-styles.js';
import * as utils from '../../js/utils.js';

class SeqSinkConfig extends LitMvvmElement {
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

  _credentialsChange(e) {
    e.stopPropagation();
    this.model.credentials[e.target.name] = utils.getFieldValue(e.target);
  }

  // first event when model is available
  beforeFirstRender() {
    //
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
    const opts = this.model.options;
    const creds = this.model.credentials;

    const result = html`
      <form>
        <section id="options" class="mb-5" @change=${this._optionsChange}>
          <fieldset>
            <legend>Options</legend>
            <div>
              <label for="serverUrl">Server Url</label>
              <input type="url" id="serverUrl" name="serverUrl" size="50" .value=${opts.serverUrl} required></input>
              <label for="proxyAddress">Proxy Address</label>
              <input type="url" id="proxyAddress" name="proxyAddress" .value=${opts.proxyAddress}></input>
            </div>
          </fieldset>
        </section>
        <section id="credentials" @change=${this._credentialsChange}>
          <fieldset>
            <legend>Credentials</legend>
            <div>
              <label for="apiKey">Api Key</label>
              <input type="text" id="apiKey" name="apiKey" .value=${creds.apiKey}></input>
            </div>
          </fieldset>
        </section>
      </form>
    `;
    return result;
  }
}

window.customElements.define('seq-sink-config', SeqSinkConfig);

const tag = model => html`<seq-sink-config .model=${model}></seq-sink-config>`;

export default tag;
