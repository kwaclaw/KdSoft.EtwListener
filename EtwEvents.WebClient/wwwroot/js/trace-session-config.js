import { html } from '../lib/lit-html.js';
import { LitMvvmElement, BatchScheduler } from '../lib/@kdsoft/lit-mvvm.js';
import { repeat } from '../lib/lit-html/directives/repeat.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { css } from '../styles/css-tag.js';
import styleLinks from '../styles/kdsoft-style-links.js';
import { SyncFusionGridStyle } from '../styles/css-grid-syncfusion-style.js';
import * as utils from './utils.js';

class TraceSessionConfig extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
  }

  connectedCallback() {
    super.connectedCallback();
  }

  firstRendered() {
    //
  }

  rendered() {
    //
  }

  _cancel(e) {
    const evt = new CustomEvent('kdsoft-cancel', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model: this.model } 
    });
    this.dispatchEvent(evt);
  }

  _apply(e) {
    const evt = new CustomEvent('kdsoft-apply', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model: this.model } 
    });
    this.dispatchEvent(evt);
  }

  static get styles() {
    return [
      css`
        #container {
          display: block;
        }
        #code-wrapper {
          color: gray;
          line-height: 1em;
        }
        #code {
          margin-left: 12ch;
          border: 1px solid LightGray;
          spell-check: false;
          color: initial;
          padding: 3px;
          line-height: 1.25em;
        }
        #code:empty::after {
          color: gray;
          content: attr(placeholder);
        }
      `,
    ];
  }

  render() {
    const filter = this.model ? this.model._filter : null;
    const result = html`
      <link rel="stylesheet" type="text/css" href=${styleLinks.tailwind} />
      <link rel="stylesheet" type="text/css" href=${styleLinks.fontawesome} />
      <style>
        :host {
          display: block;
        }
      </style>
      <div id="container">
        <div id="code-wrapper" class="border p-2"><pre>${html`using System;
using Microsoft.Diagnostics.Tracing;

namespace EtwEvents.Server
{
    public class EventFilter: IEventFilter
    {
        public bool IncludeEvent(TraceEvent evt) {
            ${html`<div id="code" contenteditable="true" spellcheck="false" placeholder="Your code goes here">${filter}</div>`}
        }
    }
}`}     </pre></div>
        <div class="flex justify-end mt-2">
          <button type="button" class="py-1 px-2" @click=${this._apply}><i class="fas fa-lg fa-check text-green-500"></i></button>
          <button type="button" class="py-1 px-2" @click=${this._cancel}><i class="fas fa-lg fa-times text-red-500"></i></button>
        </div>
      </div>
    `;
    return result;
  }
}

window.customElements.define('trace-session-config', TraceSessionConfig);
