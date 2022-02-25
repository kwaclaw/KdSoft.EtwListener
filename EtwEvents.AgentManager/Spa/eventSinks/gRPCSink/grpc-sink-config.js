import { LitMvvmElement, html, css } from '@kdsoft/lit-mvvm';
import { Queue, priorities } from '@nx-js/queue-util/dist/es.es6.js';
import tailwindStyles from '@kdsoft/lit-mvvm-components/styles/tailwind-styles.js';
import * as utils from '../../js/utils.js';

class gRPCSinkConfig extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
  }

  isValid() {
    this._setValidatedCredentials();
    const result = this.renderRoot.querySelector('form').reportValidity();
    return result;
  }

  _optionsChange(e) {
    e.stopPropagation();
    this.model.options[e.target.name] = utils.getFieldValue(e.target);
  }

  _setValidatedCredentials() {
    const checkField = this.renderRoot.getElementById('check-credentials');
    const certificatePem = utils.getFieldValue(this.renderRoot.getElementById('certificatePem'));
    const certificateKeyPem = utils.getFieldValue(this.renderRoot.getElementById('certificateKeyPem'));
    const certificateThumbPrint = utils.getFieldValue(this.renderRoot.getElementById('certificateThumbPrint'));
    const certificateSubjectCN = utils.getFieldValue(this.renderRoot.getElementById('certificateSubjectCN'));
    if (!!certificatePem && !!certificateKeyPem) {
      checkField.setCustomValidity('');
    } else if (!!certificateThumbPrint || !!certificateSubjectCN) {
      checkField.setCustomValidity('');
    } else {
      checkField.setCustomValidity('Require credentials: Certificate Pem and Key Pem, or ThumbPrint or SubjectCN.');
      return false;
    }
    this.model.certificatePem = certificatePem;
    this.model.certificateKeyPem = certificateKeyPem;
    this.model.certificateThumbPrint = certificateThumbPrint;
    this.model.certificateSubjectCN = certificateSubjectCN;
    return true;
  }

  _credentialsChange(e) {
    e.stopPropagation();
    this._setValidatedCredentials();
    e.target.reportValidity();
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

    const result = html`
      <form name="grpc-sink-config-form">
        <input type="url" id="check-url" name="url" style="display:none" />
        <section id="options" class="mb-5" @change=${this._optionsChange}>
          <fieldset>
            <legend>Options</legend>
            <div>
              <label for="host">Host</label>
              <input type="url" id="host" name="host" .value=${opts.host} required></input>
              <label for="maxSendMessageSize">MaxSendMessageSize</label>
              <input type="number" id="maxSendMessageSize" name="maxSendMessageSize" min="0" .value=${opts.maxSendMessageSize} />
              <label for="maxSendMessageSize">MaxReceiveMessageSize</label>
              <input type="number" id="maxReceiveMessageSize" name="maxReceiveMessageSize" min="0" .value=${opts.maxReceiveMessageSize} />
              <label for="maxRetryAttempts">MaxRetryAttempts</label>
              <input type="number" id="maxRetryAttempts" name="maxRetryAttempts" min="0" .value=${opts.maxRetryAttempts} />
              <label for="maxRetryBufferSize">MaxRetryBufferSize</label>
              <input type="number" id="maxRetryBufferSize" name="maxRetryBufferSize" min="0" .value=${opts.maxRetryBufferSize} />
              <label for="maxRetryBufferPerCallSize">MaxRetryBufferPerCallSize</label>
              <input type="number" id="maxRetryBufferPerCallSize" name="maxRetryBufferPerCallSize" min="0" .value=${opts.maxRetryBufferPerCallSize} />
            </div>
          </fieldset>
        </section>
        <section id="credentials" @change=${this._credentialsChange}>
          <fieldset>
            <legend>Credentials</legend>
            <div>
              <input id="check-credentials" style="display:none"></input>
              <label for="certificatePem">Certificate Pem</label>
              <textarea id="certificatePem" name="certificatePem" .value=${creds.certificatePem}></textarea>
              <label for="certificateKeyPem">Certificate Key Pem</label>
              <textarea id="certificateKeyPem" name="certificateKeyPem" .value=${creds.certificateKeyPem}></textarea>
              <label for="certificateThumbPrint">Certificate ThumbPrint</label>
              <input type="text" id="certificateThumbPrint" name="certificateThumbPrint" .value=${creds.certificateThumbPrint}></input>
              <label for="certificateSubjectCN">Certificate SubjectCN</label>
              <input type="text" id="certificateSubjectCN" name="certificateSubjectCN" .value=${creds.certificateSubjectCN}></input>
            </div>
          </fieldset>
        </section>
      </form>
    `;
    return result;
  }
}

window.customElements.define('grpc-sink-config', gRPCSinkConfig);

const tag = model => html`<grpc-sink-config .model=${model}></grpc-sink-config>`;

export default tag;
