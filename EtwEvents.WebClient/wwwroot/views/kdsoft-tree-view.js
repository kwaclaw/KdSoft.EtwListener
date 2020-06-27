import { html, nothing } from '../lib/lit-html.js';
import { repeat } from '../lib/lit-html/directives/repeat.js';
import { LitMvvmElement, css } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util/dist/es.es6.js';
import './kdsoft-expander.js';
import './kdsoft-drop-target.js';
import KdSoftDragDropProvider from './kdsoft-drag-drop-provider.js';
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
        .expander-icon {
          transition: transform var(--trans-time, 300ms) ease;
        }

        kdsoft-expander[aria-expanded]>[slot="expander"]>.expander-icon {
          transform: rotate(90deg);
        }

        .expander-grip:hover {
          cursor: grab;
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

  createTreeView(nodeModel, isLast, isRoot) {
    return html`
      ${isRoot ? nothing : html`<div is="kdsoft-drop-target" id=${nodeModel.id} data-drop-mode="before"></div>`}
      <kdsoft-expander id=${nodeModel.id} draggable="true" data-drop-mode="inside">
        <div slot="expander">
          <i class="expander-grip fas fa-xs fa-ellipsis-v text-gray-400"></i>
          <i class="expander-icon fas fa-lg fa-caret-right text-blue"></i>
        </div>
        <div slot="header">${this._getContentTemplate(nodeModel)}</div>
        <div slot="content">
          ${repeat(
            nodeModel.children,
            childModel => childModel.id,
            (childModel, index) => this.createTreeView(childModel, index === nodeModel.children.length - 1), false)
          }
        </div>
      </kdsoft-expander>
      ${isLast && !isRoot ? html`<div is="kdsoft-drop-target" id=${nodeModel.id} data-drop-mode="after"></div>` : nothing}
    `;
  }

  render() {
    return html`
      ${sharedStyles}
      ${this.createTreeView(this.model, true, true)}
    `;
  }

  rendered() {
    const draggables = this.renderRoot.querySelectorAll('kdsoft-expander');
    for (const dr of draggables) {
      if (!dr.dargdrop) {
        dr.dragdrop = new KdSoftDragDropProvider(item => item.id).connect(dr);
      }
    }
  }
}

window.customElements.define('kdsoft-tree-view', KdSoftTreeView);
