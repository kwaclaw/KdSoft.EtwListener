import { LitMvvmElement, html, css } from '@kdsoft/lit-mvvm';
import tailwindStyles from '../../styles/tailwind-styles.js';
import '../../components/valid-section.js';
import * as utils from '../../js/utils.js';

class gRPCSinkConfig extends LitMvvmElement {
  isValid() {
    const validatedSection = this.renderRoot.getElementById('credentials');
    this._setValidatedCredentials(validatedSection);
    const result = this.renderRoot.querySelector('form').reportValidity();
    return result;
  }

  _optionsChange(e) {
    e.stopPropagation();
    this.model.options[e.target.name] = utils.getFieldValue(e.target);
  }

  //TODO see https://developer.mozilla.org/en-US/docs/Web/Web_Components/Using_custom_elements
  // and see https://developer.mozilla.org/en-US/docs/Web/API/HTMLElement/attachInternals

  _setValidatedCredentials(validatedElement) {
    const certificatePem = utils.getFieldValue(this.renderRoot.getElementById('certificatePem'));
    const certificateKeyPem = utils.getFieldValue(this.renderRoot.getElementById('certificateKeyPem'));
    const certificateThumbPrint = utils.getFieldValue(this.renderRoot.getElementById('certificateThumbPrint'));
    const certificateSubjectCN = utils.getFieldValue(this.renderRoot.getElementById('certificateSubjectCN'));
    if (!!certificatePem && !!certificateKeyPem) {
      validatedElement.setCustomValidity('');
    } else if (!!certificateThumbPrint || !!certificateSubjectCN) {
      validatedElement.setCustomValidity('');
    } else {
      validatedElement.setCustomValidity('Require credentials: Certificate Pem and Key Pem, or ThumbPrint or SubjectCN.');
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
    const validatedSection = this.renderRoot.getElementById('credentials');
    this._setValidatedCredentials(validatedSection);
    validatedSection.reportValidity();
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

        valid-section {
          min-width: 100%;
          width: 100%;
        }

        valid-section fieldset {
          padding: 5px;
          border-width: 1px;
        }

        valid-section fieldset > div {
          display:grid;
          /* grid-template-columns: auto auto; */
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
        <valid-section id="options" class="mb-5" @change=${this._optionsChange}>
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
        </valid-section>
        <valid-section id="credentials" @change=${this._credentialsChange}>
          <fieldset>
            <legend>Credentials</legend>
            <div>
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
        </valid-section>
      </form>
    `;
    return result;
  }
}

window.customElements.define('grpc-sink-config', gRPCSinkConfig);

const tag = model => html`<grpc-sink-config .model=${model}></grpc-sink-config>`;

export default tag;
