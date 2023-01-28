/* global i18n */

import { LitMvvmElement, html, css, nothing, BatchScheduler } from '@kdsoft/lit-mvvm';
import fontAwesomeStyles from '../styles/fontawesome/css/all-styles.js';
import tailwindStyles from '../styles/tailwind-styles.js';
import appStyles from '../styles/etw-app-styles.js';

class ErrorCount extends LitMvvmElement {
  shouldRender() {
    return !!this.model;
  }

  static get styles() {
    return [
      tailwindStyles,
      fontAwesomeStyles,
      appStyles
    ];
  }

  render() {
    return html`${this.model.fetchErrors.count()} ${i18n.__('Errors')}`;
  }
}

window.customElements.define('error-count', ErrorCount);
