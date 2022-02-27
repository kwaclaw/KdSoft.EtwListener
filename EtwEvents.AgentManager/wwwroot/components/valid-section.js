export default class ValidSection extends HTMLElement {
  static get formAssociated() { return true; }

  constructor() {
    super();
    this.tabIndex = -1;
    this._internals = this.attachInternals();
  }

  // The following properties and methods aren't strictly required,  but native form controls provide them.
  // Providing them helps ensure consistency with native controls.
  get form() { return this._internals.form; }
  get name() { return this.getAttribute('name'); }
  get type() { return this.localName; }
  get validity() { return this._internals.validity; }
  get validationMessage() { return this._internals.validationMessage; }
  get willValidate() { return this._internals.willValidate; }

  checkValidity() { return this._internals.checkValidity(); }
  reportValidity() { return this._internals.reportValidity(); }
  setValidity(flags, message, anchor) { this._internals.setValidity(flags, message, anchor); }
  setCustomValidity(message) {
    if (message) {
      this._internals.setValidity({ customError: true }, message);
    } else {
      this._internals.setValidity({});
      this.style.border = 'unset';
    }
  }

  _showInvalid(e) {
    this.style.border = '2px solid red';
  }

  connectedCallback() {
    this.style.display = 'block';
    this.removeEventListener('invalid', this._showInvalid);
    this.addEventListener('invalid', this._showInvalid);
  }
}

window.customElements.define('valid-section', ValidSection);
