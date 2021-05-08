import { html, nothing } from 'lit';
import { Queue, priorities } from '@nx-js/queue-util/dist/es.es6.js';
import { LitMvvmElement, css } from '@kdsoft/lit-mvvm';
import '@kdsoft/lit-mvvm-components/kdsoft-dropdown.js';
import '@kdsoft/lit-mvvm-components/kdsoft-checklist.js';
import '@kdsoft/lit-mvvm-components/kdsoft-expander.js';
import '@kdsoft/lit-mvvm-components/kdsoft-drop-target.js';
import '@kdsoft/lit-mvvm-components/kdsoft-tree-view.js';
import tailwindStyles from '@kdsoft/lit-mvvm-components/styles/tailwind-styles.js';
import checkboxStyles from '@kdsoft/lit-mvvm-components/styles/kdsoft-checkbox-styles.js';
import fontAwesomeStyles from '@kdsoft/lit-mvvm-components/styles/fontawesome/css/all-styles.js';
import appStyles from '../styles/etw-app-styles.js';

class FileSinkConfig extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
  }

  _cancel() {
    //
  }

  _apply() {
    //
  }

  _export() {
    //
  }

  /* eslint-disable indent, no-else-return */

  connectedCallback() {
    super.connectedCallback();
    // this.addEventListener('kdsoft-node-move', this.rootNode.moveNode);
  }

  disconnectedCallback() {
    // this.removeEventListener('kdsoft-node-move', this.rootNode.moveNode);
    super.disconnectedCallback();
  }

  // shouldRender() {
  //   const result = !!this.model;
  //   return result;
  // }

  beforeFirstRender() {
    // model is defined, because of our shouldRender() override
  }

  firstRendered() {
    //
  }

  rendered() {
    //
  }

  static get styles() {
    return [
      tailwindStyles,
      checkboxStyles,
      fontAwesomeStyles,
      appStyles,
      css`
        `,
    ];
  }

  render() {
    const result = html`
      <style>
        :host {
          display: block;
        }
      </style>
      <form>
        <h3>File Sink</h3>
      </form>
    `;
    return result;
  }
}

window.customElements.define('file-sink-config', FileSinkConfig);

const tagName = html`<file-sink-config></file-sink-config>`;

export default tagName;
