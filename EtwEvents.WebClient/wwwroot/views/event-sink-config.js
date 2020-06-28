import { html, nothing } from '../lib/lit-html.js';
import { repeat } from '../lib/lit-html/directives/repeat.js';
import { LitMvvmElement, css } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util/dist/es.es6.js';
import * as utils from '../js/utils.js';
import sharedStyles from '../styles/kdsoft-shared-styles.js';
import styleLinks from '../styles/kdsoft-style-links.js';
import '../components/kdsoft-dropdown.js';
import '../components/kdsoft-checklist.js';
import '../components/kdsoft-expander.js';
import '../components/kdsoft-drop-target.js';
import '../components/kdsoft-tree-view.js';
import KdSoftDropdownModel from '../components/kdsoft-dropdown-model.js';
import KdSoftDropdownChecklistConnector from '../components/kdsoft-dropdown-checklist-connector.js';
import KdSoftTreeNodeModel from '../components/kdsoft-tree-node-model.js';

function getSelectedSinkTypeText(checkListModel) {
  let result = null;
  for (const selEntry of checkListModel.selectedEntries) {
    if (result) result += `, ${selEntry.item.name}`;
    else result = selEntry.item.name;
  }
  return result;
}

class EventSinkConfig extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
    this.sinkTypeDropDownModel = new KdSoftDropdownModel();
    this.sinkTypeChecklistConnector = new KdSoftDropdownChecklistConnector(
      () => this.renderRoot.getElementById('sinkType'),
      () => this.renderRoot.getElementById('sinkTypeList'),
      getSelectedSinkTypeText
    );

    this.rootNode = this._createTreeModel();
  }

  _createTreeModel() {
    const grandChildren = [];
    for (let indx = 0; indx < 15; indx += 1) {
      const nodeModel = new KdSoftTreeNodeModel(`3-${indx}`, [], { type: 'gc', text: `Grand child blah blah ${indx}` });
      grandChildren.push(nodeModel);
    }

    const children = [];
    for (let indx = 0; indx < 5; indx += 1) {
      const gci = indx * 3;
      const nodeModel = new KdSoftTreeNodeModel(`2-${indx}`, grandChildren.slice(gci, gci + 3), { type: 'c', text: `Child blah blah ${indx}` });
      children.push(nodeModel);
    }

    return new KdSoftTreeNodeModel('0-0', children, { type: 'r', text: `Root Node` });
  }

  _cancel() {
    const evt = new CustomEvent('kdsoft-done', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model: this.model, canceled: true }
    });
    this.dispatchEvent(evt);
  }

  _apply() {
    const valid = this.renderRoot.querySelector('form').reportValidity();
    if (!valid) return;

    const evt = new CustomEvent('kdsoft-done', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model: this.model, canceled: false }
    });
    this.dispatchEvent(evt);
  }

  _export() {
    this.model.export();
  }

  /* eslint-disable indent, no-else-return */

  _getTreeNodeContentTemplate(nodeModel) {
    let cls = '';
    switch (nodeModel.type) {
      case 'gc':
        cls = 'text-red-600';
        break;
      case 'c':
        cls = 'text-blue-600';
        break;
      case 'r':
        cls = 'text-black-600';
        break;
      default:
        break;
    }
    return html`<span class=${cls}>${nodeModel.text}</span>`;
  }

  connectedCallback() {
    super.connectedCallback();
    // this.addEventListener('kdsoft-node-move', this.rootNode.moveNode);
  }

  disconnectedCallback() {
    // this.removeEventListener('kdsoft-node-move', this.rootNode.moveNode);
    super.disconnectedCallback();
  }

  shouldRender() {
    const result = !!this.model;
    return result;
  }

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
        form {
          position: relative;
          height: 100%;
          display: flex;
          flex-direction: column;
          align-items: stretch;
        }

        #container {
          position: relative;
          flex: 1 1 auto;
          overflow-y: auto;
        }

        section {
          position: relative;
        }

        section:not(.active) {
          display: none !important;
        }

        #general {
          display: grid;
          grid-template-columns: 1fr 2fr;
          align-items: baseline;
          align-content: start;
          grid-gap: 5px;
          min-width: 480px;
        }

        fieldset {
          display: contents;
        }

        label {
          font-weight: bolder;
          color: #718096;
        }

        #filters {
          height: 100%;
        }

        #ok-cancel-buttons {
          margin-top: auto;
        }

        #name:invalid, #host:invalid, #lifeTime:invalid {
          border: 2px solid red;
        }

        #standard-cols-wrapper {
          position: relative;
          width: 40%;
        }

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
        <nav class="flex flex-col sm:flex-row mb-4">
          <kdsoft-dropdown id="sinkType" class="py-0" .model=${this.sinkTypeDropDownModel} .connector=${this.sinkTypeChecklistConnector}>
            <kdsoft-checklist
              id="sinkTypeList" 
              class="text-black" 
              .model=${this.model.sinkTypeCheckListModel}
              .getItemTemplate=${item => html`${item.name}`}>
            </kdsoft-checklist>
          </kdsoft-dropdown>
        </nav>
        
        <div id="container" class="mb-4 relative">
          <kdsoft-tree-view
            .model=${this.rootNode}
            .contentTemplateCallback=${this._getTreeNodeContentTemplate}
            style="max-width:400px;max-height:100%;overflow-y:auto;"
          ></kdsoft-tree-view>
        </div>

        <hr class="mb-4" />
        <div id="ok-cancel-buttons" class="flex flex-wrap mt-2 bt-1">
          <button type="button" class="py-1 px-2" @click=${this._export} title="Export">
            <i class="fas fa-lg fa-file-export text-gray-600"></i>
          </button>
          <button type="button" class="py-1 px-2 ml-auto" @click=${this._apply} title="Save">
            <i class="fas fa-lg fa-check text-green-500"></i>
          </button>
          <button type="button" class="py-1 px-2" @click=${this._cancel} title="Cancel" autofocus>
            <i class="fas fa-lg fa-times text-red-500"></i>
          </button>
        </div>
      </form>
    `;
    return result;
  }
}

window.customElements.define('event-sink-config', EventSinkConfig);
