/* global i18n */

import { html, nothing } from 'lit';
import { LitMvvmElement, css, BatchScheduler } from '@kdsoft/lit-mvvm';
import fontAwesomeStyles from '@kdsoft/lit-mvvm-components/styles/fontawesome/css/all-styles.js';
import tailwindStyles from '../styles/tailwind-styles.js';
import appStyles from '../styles/etw-app-styles.js';

class ErrorCount extends LitMvvmElement {
  constructor() {
    super();
    // seems priorities.HIGH may not allow render() calls in child components in some scenarios
    //this.scheduler = new Queue(priorities.LOW);
    //this.scheduler = new BatchScheduler(0);
    this.scheduler = window.renderScheduler;
  }

  //#region overrides

  /* eslint-disable indent, no-else-return */

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

  //#endregion
}

window.customElements.define('error-count', ErrorCount);
