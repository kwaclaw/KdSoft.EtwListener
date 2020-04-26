import { html } from '../lib/lit-html.js';
import { LitMvvmElement, BatchScheduler } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { css } from '../styles/css-tag.js';
import sharedStyles from '../styles/kdsoft-shared-styles.js';

function toggleExpansion(element, doExpand) {
  // get the height of the element's inner content, regardless of its actual size
  const height = element.scrollHeight;
  const style = element.style;

  style.height = doExpand ? '0px' : `${height}px`;

  // on the next frame (as soon as the previous style change has taken effect), explicitly set
  // the element's height to its current pixel height, so we aren't transitioning out of 'auto'
  requestAnimationFrame(() => {
    style.transition = 'height var(--trans-time, 300ms) ease';

    element.addEventListener('transitionend', function resetHeight() {
      element.removeEventListener('transitionend', resetHeight);
      style.height = null;
      style.transition = null;
    });

    // on the next frame (as soon as the previous style change has taken effect),
    // have the element transition to its content height
    requestAnimationFrame(() => {
      style.height = doExpand ? `${height}px` : '0px';
    });
  });
}


class KdSoftTreeNode extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
  }

  get ariaExpanded() { return this.hasAttribute('aria-expanded'); }
  set ariaExpanded(val) {
    if (val) this.setAttribute('aria-expanded', ''); else this.removeAttribute('aria-expanded');
  }

  // Observed attributes will trigger an attributeChangedCallback, which in turn will cause a re-render to be scheduled!
  static get observedAttributes() {
    return [...super.observedAttributes, 'aria-expanded'];
  }

  static get styles() {
    return [
      css`
        #container {
          display: grid;
          grid-template-columns: max-content minmax(0, 1fr);
          padding: var(--content-padding, 5px);
        }

        #container.droppable {
          outline: 2px solid darkgray;
        }

        #expander {
          display: flex;
          justify-content: center;
          align-items: center;
          cursor: pointer;
          width: var(--left-padding, 2em);
        }

        #expander:focus {
          outline: none;
        }

        #expander i {
          transition: transform var(--trans-time, 300ms) ease;
        }

        :host([aria-expanded]) #expander i {
          transform: rotate(90deg);
        }

        #children-slot {
          overflow: hidden;
          height: 0;
          border-color: darkgray;
        }

        :host([aria-expanded]) #children-slot {
          height: unset;
        }
      `,
    ];
  }

  attributeChangedCallback(name, oldValue, newValue) {
    if (name === 'aria-expanded') {
      const children = this.renderRoot.getElementById('children-slot');
      if (!children) return;

      toggleExpansion(children, newValue !== null);
    }
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

  _expanderClicked() {
    this.ariaExpanded = !this.ariaExpanded;
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
    const result = html`
      ${sharedStyles}
      <style>
        :host {
          display: block;
        }
      </style>
      <div id="container">
        <div id="expander" tabindex="1" @click=${this._expanderClicked}>
          <i part="expander-icon" class="fas fa-lg fa-caret-right text-blue"></i>
        </div>
        <div id="content-slot">
          <slot name="content" tabindex="2">No node content provided.</slot>
        </div>
        <div id="leftbar"></div>
        <div id="children-slot" class="border-l-2 pl-2">
          <slot name="children" tabindex="3">No children provided.</slot>
        </div>
      </div>
    `;
    return result;
  }
}

window.customElements.define('kdsoft-tree-node', KdSoftTreeNode);
