import { html, nothing } from '../lib/lit-html.js';
import { repeat } from '../lib/lit-html/directives/repeat.js';
import { LitMvvmElement, css } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util/dist/es.es6.js';
import './kdsoft-expander.js';
import './kdsoft-drop-target.js';
import sharedStyles from '../styles/kdsoft-shared-styles.js';

class KdSoftTreeView extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
  }

  get contentTemplateCallback() { return this._getContentTemplate; }
  set contentTemplateCallback(value) { this._getContentTemplate = value; }

  _moveNode(e) {
    const eventNode = e.composedPath()[0];
    const dropMode = eventNode.dataset.dropMode;
    this.model.moveNode(e.detail.fromId, e.detail.toId, dropMode);
  }

  /* eslint-disable indent, no-else-return */

  connectedCallback() {
    this.addEventListener('kdsoft-node-move', this._moveNode);
    super.connectedCallback();
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this.removeEventListener('kdsoft-node-move', this._moveNode);
  }

  /* https://philipwalton.com/articles/what-no-one-told-you-about-z-index/ */
  static get styles() {
    return [
      css`
        kdsoft-expander {
          /* z-index: auto; */
        }

        [data-drop-mode].droppable {
          outline: 2px solid lightblue;
          outline-offset: -2px;
        }

        div[data-drop-mode] {
          height: 1em;
          margin: -0.5em 0 -0.5em 0;
          padding: 0;
          position: relative;
          z-index:10;
        }
      `,
    ];
  }

  createTreeView(nodeModel, isLast) {
    return html`
      <div is="kdsoft-drop-target" id=${nodeModel.id} data-drop-mode="before"></div>
      <kdsoft-expander id=${nodeModel.id} draggable="true" data-drop-mode="inside">
        <div slot="header">${this._getContentTemplate(nodeModel)}</div>
        <div slot="content">
          ${repeat(
            nodeModel.children,
            childModel => childModel.id,
            (childModel, index) => this.createTreeView(childModel, index === nodeModel.children.length - 1))
          }
        </div>
      </kdsoft-expander>
      ${isLast ? html`<div is="kdsoft-drop-target" id=${nodeModel.id} data-drop-mode="after"></div>` : nothing}
    `;
  }

  render() {
    return html`
      ${sharedStyles}
      ${this.createTreeView(this.model, true)}
    `;
  }
}

window.customElements.define('kdsoft-tree-view', KdSoftTreeView);
