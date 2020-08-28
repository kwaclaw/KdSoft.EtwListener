import { html, nothing } from '../lib/lit-html.js';
import { LitMvvmElement, css } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util/dist/es.es6.js';
import sharedStyles from '../styles/kdsoft-shared-styles.js';
import styleLinks from '../styles/kdsoft-style-links.js';
import '../components/kdsoft-dropdown.js';
import '../components/kdsoft-checklist.js';
import '../components/kdsoft-expander.js';
import '../components/kdsoft-drop-target.js';
import '../components/kdsoft-tree-view.js';

class MongoSinkConfig extends LitMvvmElement {
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
      css`
        `,
    ];
  }

  render() {
    const result = html`
      ${sharedStyles}
      <link rel="stylesheet" type="text/css" href=${styleLinks.checkbox} />
      <style>
        :host {
          display: block;
        }
      </style>
      <form>
        <h3>Mongo Sink</h3>
      </form>
    `;
    return result;
  }
}

window.customElements.define('mongo-sink-config', MongoSinkConfig);

const tagName = html`<mongo-sink-config></mongo-sink-config>`;

export default tagName;
