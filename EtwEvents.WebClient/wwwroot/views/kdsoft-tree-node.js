import { html } from '../lib/lit-html.js';
import { LitMvvmElement, BatchScheduler } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { classMap } from '../lib/lit-html/directives/class-map.js';
import { css } from '../styles/css-tag.js';
import sharedStyles from '../styles/kdsoft-shared-styles.js';

const expanderBase = { fas: true, 'fa-lg': true, 'text-blue-500': true };

const classList = {
  expanderUp: { ...expanderBase, 'fa-caret-right': true },
  expanderDown: { ...expanderBase, 'fa-caret-down': true },
};

class KdSoftTreeNode extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
  }

  get expanded() { return this.hasAttribute('expanded'); }
  set expanded(val) {
    if (val) this.setAttribute('expanded', ''); else this.removeAttribute('expanded');
  }

  // Observed attributes will trigger an attributeChangedCallback, which in turn will cause a re-render to be scheduled!
  static get observedAttributes() {
    return [...super.observedAttributes, 'expanded'];
  }

  static get styles() {
    return [
      css`
        #container {
          display: grid;
          grid-template-columns: 30px 1fr;
          grid-template-rows: auto;
        }
        #container.droppable {
          outline: 2px solid darkgray;
        }

        .expander {
          grid-area: 1/1/1/1;
          height: 30px;
          display: flex;
          justify-content: center;
          align-items: center;
          cursor: pointer;
        }
        .leftbar {
          grid-area: 2/1/-1/1;
        }
        .node-content {
          padding: var(--content-padding, 5px);
        }
        slot[name="content"] {
          grid-area: 1/2/1/3;
        }
        slot[name="children"] {
          grid-area: 2/2/2/3;
        }
        .children {
          display: contents;
        }
        .children-hidden {
          display: none;
        }
      `,
    ];
  }

  connectedCallback() {
    super.connectedCallback();

    const h = this.shadowRoot.host;
    h.setAttribute('draggable', true);
    h.addEventListener('dragstart', this._dragStart);
    h.addEventListener('dragenter', this._dragEnter);
    h.addEventListener('dragover', this._dragOver);
    h.addEventListener('dragleave', this._dragLeave);
    h.addEventListener('drop', this._drop);
  }

  disconnectedCallback() {
    const h = this.shadowRoot.host;
    h.removeEventListener('dragstart', this._dragStart);
    h.removeEventListener('dragenter', this._dragEnter);
    h.removeEventListener('dragover', this._dragOver);
    h.removeEventListener('dragleave', this._dragLeave);
    h.removeEventListener('drop', this._drop);

    super.disconnectedCallback();
  }

  firstRendered() {
    //
  }

  rendered() {
    //
  }

  _expanderClicked(e) {
    this.expanded = !this.expanded;
  }

  _dragStart(e) {
    e.stopPropagation();
    e.dataTransfer.setData('text/plain', e.currentTarget.id);
    e.dataTransfer.effectAllowed = 'move';
  }

  _dragOver(e) {
    e.preventDefault();
  }

  // need to maintain a drag enter counter for the item, as the drag enter/leave event happen when a child
  // node is being moved over and we know only that we left the parent element when the counter reaches 0.
  _dragEnter(e) {
    e.stopPropagation();
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';

    const item = e.currentTarget;
    const dragEnterCount = item._dragEnterCount || 0;
    if (dragEnterCount <= 0) {
      item._dragEnterCount = 1;
      this.shadowRoot.getElementById('container').classList.add('droppable');
    } else {
      item._dragEnterCount = dragEnterCount + 1;
    }
  }

  _dragLeave(e) {
    e.stopPropagation();
    e.preventDefault();

    const item = e.currentTarget;
    const dragEnterCount = item._dragEnterCount || 0;
    item._dragEnterCount = dragEnterCount - 1;

    if (item._dragEnterCount <= 0) {
      this.shadowRoot.getElementById('container').classList.remove('droppable');
    }
  }

  _drop(e) {
    e.stopPropagation();
    e.preventDefault();

    const item = e.currentTarget;
    item._dragEnterCount = 0;
    this.shadowRoot.getElementById('container').classList.remove('droppable');

    const fromData = e.dataTransfer.getData('text/plain');
    const toData = item.id;

    const evt = new CustomEvent('kdsoft-node-move', { bubbles: true, cancelable: true, detail: { fromId: fromData, toId: toData } });
    this.dispatchEvent(evt);
  }

  /* eslint-disable indent, no-else-return */

  render() {
    const expanderClasses = this.expanded ? classList.expanderDown : classList.expanderUp;
    const childrenClass = this.expanded ? 'children' : 'children-hidden';
    const result = html`
      ${sharedStyles}
      <style>
        :host {
          display: block;
        }
      </style>
      <div id="container" class="border">
        <div id="expander" class="expander" tabindex="1" @click=${this._expanderClicked}>
          <i class=${classMap(expanderClasses)}></i>
        </div>
        <div class="node-content">
          <slot name="content" tabindex="2">No node content provided.</slot>
        </div>
        <div class=${childrenClass}>
          <div class="leftbar"></div>
          <slot name="children" tabindex="3">No children provided.</slot>
        </div>
      </div>
    `;
    return result;
  }
}

window.customElements.define('kdsoft-tree-node', KdSoftTreeNode);
