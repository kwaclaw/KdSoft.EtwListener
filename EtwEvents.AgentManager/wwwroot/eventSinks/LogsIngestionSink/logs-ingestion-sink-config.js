import { LitMvvmElement, html, css } from '@kdsoft/lit-mvvm';
import tailwindStyles from '../../styles/tailwind-styles.js';
import * as utils from '../../js/utils.js';

class LogsIngestionSinkConfig extends LitMvvmElement {
  isValid() {
    return this.renderRoot.querySelector('form').reportValidity();
  }

  _optionsChange(e) {
    e.stopPropagation();
    this.model.options[e.target.name] = utils.getFieldValue(e.target);
  }

  _credentialsChange(e) {
    e.stopPropagation();
    const fieldValue = utils.getFieldValue(e.target);
    utils.setFieldValue(this.model.credentials, e.target.name, fieldValue);
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
              <label for="endPoint">Endpoint URL</label>
              <input type="url" id="endPoint" name="endPoint" .value=${opts.endPoint} required></input>
              <label for="ruleId">Rule Id</label>
              <input type="text" id="ruleId" name="ruleId" .value=${opts.ruleId} required></input>
              <label for="streamName">Stream Name</label>
              <input type="text" id="streamName" name="streamName" .value=${opts.streamName} required></input>
            </div>
          </fieldset>
        </section>
        <section id="credentials" @change=${this._credentialsChange}>
          <fieldset>
            <legend>Credentials</legend>
            <div>
              <label for="tenantId">Tenant Id</label>
              <input type="text" id="tenantId" name="tenantId" .value=${creds.tenantId}></input>
              <label for="clientId">Client Id</label>
              <input type="text" id="clientId" name="clientId" .value=${creds.clientId}></input>
            </div>
            <fieldset>
              <legend>Client Secret</legend>
              <div>
                <label for="clientSecret">Secret</label>
                <input type="password" id="clientSecret" name="clientSecret.secret" .value=${creds.clientSecret.secret}></input>
              </div>
            </fieldset>
            <fieldset>
              <legend>Client Certificate</legend>
              <div>
                <label for="certificatePem">Certificate Pem</label>
                <textarea id="certificatePem" name="certificatePem" .value=${creds.clientCertificate.certificatePem}></textarea>
                <label for="certificateKeyPem">Certificate Key Pem</label>
                <textarea id="certificateKeyPem" name="certificateKeyPem" .value=${creds.clientCertificate.certificateKeyPem}></textarea>
                <label for="certificateThumbPrint">Certificate ThumbPrint</label>
                <input type="text" id="certificateThumbPrint" name="certificateThumbPrint" .value=${creds.clientCertificate.certificateThumbprint}></input>
                <label for="certificateSubjectCN">Certificate SubjectCN</label>
                <input type="text" id="certificateSubjectCN" name="certificateSubjectCN" .value=${creds.clientCertificate.certificateSubjectCN}></input>
              </div>
            </fieldset>
            <fieldset>
              <legend>User Credentials</legend>
              <div>
                <label for="username">User Name</label>
                <input type="text" id="username" name="username" .value=${creds.usernamePassword.username}></input>
                <label for="password">Password</label>
                <input type="password" id="password" name="password" .value=${creds.usernamePassword.password}></input>
              </div>
            </fieldset>
          </fieldset>
        </section>
      </form>
    `;
    return result;
  }
}

window.customElements.define('logs-ingestion-sink-config', LogsIngestionSinkConfig);

const tag = model => html`<logs-ingestion-sink-config .model=${model}></logs-ingestion-sink-config>`;

export default tag;
