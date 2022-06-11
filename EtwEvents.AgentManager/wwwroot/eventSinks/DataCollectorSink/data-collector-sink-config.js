import { LitMvvmElement, html, css, BatchScheduler } from '@kdsoft/lit-mvvm';
import { Queue, priorities } from '@nx-js/queue-util';
import tailwindStyles from '@kdsoft/lit-mvvm-components/styles/tailwind-styles.js';
import * as utils from '../../js/utils.js';

class DataCollectorSinkConfig extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = window.renderScheduler;
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
              <label for="customerId">Customer Id</label>
              <input type="text" id="customerId" name="customerId" .value=${opts.customerId} required maxlength="100"></input>
              <label for="logType">Log Type</label>
              <input type="text" id="logType" name="logType" .value=${opts.logType}></input>
              <label for="resourceId">Resource Id</label>
              <input type="text" id="resourceId" name="resourceId" .value=${opts.resourceId}></input>
            </div>
          </fieldset>
        </section>
        <section id="credentials" @change=${this._credentialsChange}>
          <fieldset>
            <legend>Credentials</legend>
            <div>
              <label for="sharedKey">Shared Key</label>
              <input type="password" id="sharedKey" name="sharedKey" .value=${creds.sharedKey} required></input>
            </div>
          </fieldset>
        </section>
      </form>
    `;
    return result;
  }
}

window.customElements.define('data-collector-sink-config', DataCollectorSinkConfig);

const tag = model => html`<data-collector-sink-config .model=${model}></data-collector-sink-config>`;

export default tag;
