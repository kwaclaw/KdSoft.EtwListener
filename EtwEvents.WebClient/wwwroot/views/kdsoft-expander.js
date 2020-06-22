import { html, nothing } from '../lib/lit-html.js';
import { LitMvvmElement, css } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util/dist/es.es6.js';
import sharedStyles from '../styles/kdsoft-shared-styles.js';
import KdSoftDragDropProvider from './kdsoft-drag-drop-provider.js';

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


class KdSoftExpander extends LitMvvmElement {
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
    return [...super.observedAttributes, 'aria-expanded', 'draggable'];
  }

  attributeChangedCallback(name, oldValue, newValue) {
    if (name === 'aria-expanded') {
      const children = this.renderRoot.getElementById('content-slot');
      if (!children) return;
      toggleExpansion(children, newValue !== null);
    } else if (name === 'draggable') {
      this._enableDragDrop(newValue === 'true');
    }
  }

  _enableDragDrop(enable) {
    if (enable) {
      if (!this.dragdrop) {
        this.dragdrop = new KdSoftDragDropProvider(item => item.id);
        this.dragdrop.connect(this.renderRoot.host);
      }
    } else if (this.dragdrop) {
      this.dragdrop.disconnect();
      this.dragdrop = null;
    }
  }

  _expanderClicked() {
    this.ariaExpanded = !this.ariaExpanded;
  }

  /* eslint-disable indent, no-else-return */

  connectedCallback() {
    // run this before render gets called from super.connectedCallback()
    const draggable = this.renderRoot.host.getAttribute('draggable');
    this._enableDragDrop(draggable === 'true');

    super.connectedCallback();
  }

  disconnectedCallback() {
    super.disconnectedCallback();

    if (this.dragdrop) {
      this.dragdrop.disconnect();
      this.dragdrop = null;
    }
  }

  static get styles() {
    return [
      css`
        /* :host(.droppable) {
          outline: 2px solid darkgray;
          outline-offset: -2px;
        } */

        #container {
          display: grid;
          grid-template-columns: max-content minmax(0, 1fr);
          padding: var(--content-padding, 5px);
        }

        #expander {
          display: flex;
          align-items: center;
          justify-content: space-evenly;
          cursor: pointer;
          width: var(--left-padding, 2rem);
        }

        #expander:focus {
          outline: none;
        }

        #expander i[part=expander-grip]:hover {
          cursor: grab;
        }

        #expander i[part=expander-icon] {
          transition: transform var(--trans-time, 300ms) ease;
        }

        :host([aria-expanded]) #expander i[part=expander-icon] {
          transform: rotate(90deg);
        }

        #content-slot {
          overflow: hidden;
          height: 0;
          border-color: darkgray;
        }

        :host([aria-expanded]) #content-slot {
          height: unset;
        }
      `,
    ];
  }

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
          ${this.dragdrop
            ? html`<i part="expander-grip" class="fas fa-xs fa-ellipsis-v text-gray-400"></i>`
            : nothing
          }
          <i part="expander-icon" class="fas fa-lg fa-caret-right text-blue"></i>
        </div>
        <div id="header-slot">
          <slot name="header" tabindex="2">No header provided.</slot>
        </div>
        <div id="leftbar"></div>
        <div id="content-slot" class="border-l-2 pl-1">
          <slot name="content" tabindex="3">No content provided.</slot>
        </div>
      </div>
    `;
    return result;
  }
}

window.customElements.define('kdsoft-expander', KdSoftExpander);
