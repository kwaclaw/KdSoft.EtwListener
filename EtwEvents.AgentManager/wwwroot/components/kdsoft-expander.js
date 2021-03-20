import { html } from '../lib/lit-html.js';
import { LitMvvmElement, css } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util/dist/es.es6.js';
import sharedStyles from '../styles/kdsoft-shared-styles.js';

function updateExpansion(element, doExpand) {
  // get the height of the element's inner content, regardless of its actual size
  const height = element.scrollHeight;
  const style = element.style;

  const oldHeight = style.height;
  style.height = doExpand ? '0px' : `${height}px`;

  // when expanding to the new height of '0px', then the 'transitionend' event would not be
  // triggered as there would not a change in height, so we run the code directly, no transiton needed
  if (doExpand && oldHeight === style.height) {
    style.height = null;
    style.transition = null;
    return;
  }

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
    return [...super.observedAttributes, 'aria-expanded'];
  }

  attributeChangedCallback(name, oldValue, newValue) {
    if (name === 'aria-expanded') {
      const children = this.renderRoot.getElementById('content-slot');
      if (!children) return;
      updateExpansion(children, newValue !== null);
    }
    super.attributeChangedCallback(name, oldValue, newValue);
  }

  _expanderClicked() {
    this.ariaExpanded = !this.ariaExpanded;
  }

  /* eslint-disable indent, no-else-return */

  static get styles() {
    return [
      css`
        :host {
          display: block;
        }

        #container {
          display: grid;
          grid-template-columns: max-content minmax(0, 1fr);
          padding: var(--content-padding, 5px);
        }

        #expander, #expander::slotted(div) {
          display: flex;
          align-items: center;
          justify-content: space-evenly;
          cursor: pointer;
          width: var(--left-padding, 2rem);
        }

        #expander:focus {
          outline: none;
        }

        #expander-icon {
          transition: transform var(--trans-time, 300ms) ease;
        }

        :host([aria-expanded]) #expander-icon {
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
      <div id="container">
        <slot name="expander" id="expander" tabindex="1" @click=${this._expanderClicked}>
          <i id="expander-icon" class="fas fa-lg fa-caret-right text-blue"></i>
        </slot>
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
