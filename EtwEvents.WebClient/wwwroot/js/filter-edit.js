﻿import { html } from '../lib/lit-html.js';
import { LitMvvmElement } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { css } from '../styles/css-tag.js';
import styleLinks from '../styles/kdsoft-style-links.js';

class FilterEdit extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.LOW);
  }

  _change(e) {
    const filter = e.currentTarget.value;
    this.model._filter = filter;
    //e.currentTarget.innerHTML = '';
  }

  static get styles() {
    return [
      css`
        #code-wrapper {
          color: gray;
          line-height: 1em;
        }
        #code {
          margin-left: 0;
          border: 1px solid LightGray;
          spell-check: false;
          color: initial;
          padding: 3px;
          line-height: 1.25em;
          font: inherit;
          resize: both;
        }
        /* only needed for contenteditable elements
        #code:empty::after {
          color: gray;
          content: attr(placeholder);
        }
        */
      `,
    ];
  }

  render() {
    const filter = this.model ? this.model.filter : null;
    const result = html`
      <link rel="stylesheet" type="text/css" href=${styleLinks.tailwind} />
      <link rel="stylesheet" type="text/css" href=${styleLinks.fontawesome} />
      <style>
        :host {
          display: block;
        }
      </style>
      <div id="code-wrapper" class="border p-2"><pre>${html`using System;
using Microsoft.Diagnostics.Tracing;

namespace EtwEvents.Server
{
    public class EventFilter: IEventFilter
    {
        public bool IncludeEvent(TraceEvent evt) {
            ${html`<textarea id="code" @blur=${this._change} rows="10" cols="80" spellcheck="false" placeholder="Your code goes here">${filter}</textarea>`}
        }
    }
}`}   </pre></div>
    `;
    return result;
  }
}

window.customElements.define('filter-edit', FilterEdit);