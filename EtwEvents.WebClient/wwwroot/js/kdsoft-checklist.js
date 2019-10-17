import { html, nothing } from '../lib/lit-html.js';
import { repeat } from '../lib/lit-html/directives/repeat.js';
import { classMap } from '../lib/lit-html/directives/class-map.js';
import { LitMvvmElement, BatchScheduler } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { css } from '../styles/css-tag.js';
import styleLinks from '../styles/kdsoft-style-links.js';

const arrowBase = { far: true, 'fa-lg': true, 'text-blue-500': true, 'align-text-bottom': true, 'pt-1': true };

const classList = {
  upArrowVisible: { ...arrowBase, 'fa-caret-square-up': true },
  upArrowHidden: { ...arrowBase, 'fa-caret-square-up': true, invisible: true },
  downArrowVisible: { ...arrowBase, 'fa-caret-square-down': true },
  downArrowHidden: { ...arrowBase, 'fa-caret-square-down': true, invisible: true },
};

class KdSoftCheckList extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
    //this.scheduler = new BatchScheduler(0);
  }

  get showCheckboxes() { return this.hasAttribute('show-checkboxes'); }
  set showCheckboxes(val) { if (val) this.setAttribute('show-checkboxes', ''); else this.removeAttribute('show-checkboxes'); }

  get arrows() { return this.hasAttribute('arrows'); }
  set arrows(val) { if (val) this.setAttribute('arrows', ''); else this.removeAttribute('arrows'); }

  get allowDragDrop() { return this.hasAttribute('allow-drag-drop'); }
  set allowDragDrop(val) { if (val) this.setAttribute('allow-drag-drop', ''); else this.removeAttribute('allow-drag-drop'); }

  // Observed attributes will trigger an attributeChangedCallback, which in turn will cause a re-render to be scheduled!
  static get observedAttributes() {
    return [...super.observedAttributes, 'arrows', 'allow-drag-drop'];
  }

  static get styles() {
    return [
      css`
        #container {
          display: flex;
          align-items: baseline;
          justify-items: flex-end;
        }
        #item-list {
          display: inline-block;
          position: relative;
          -webkit-overflow-scrolling: touch; /* Lets it scroll lazy */
          padding: 5px;
          max-height: var(--max-scroll-height, 300px);
        }
        .droppable {
          outline: 2px solid darkgray;
        }
      `,
    ];
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

  _checkboxClicked(e) {
    const itemDiv = e.currentTarget.closest('.list-item');
    // if we don't want toggle on single-select
    if (!this.model.multiSelect) e.currentTarget.checked = true;
    this.model.selectIndex(itemDiv.dataset.itemIndex, e.currentTarget.checked);
  }

  _dragStart(e) {
    e.dataTransfer.setData('text/plain', e.currentTarget.dataset.itemIndex);
    e.dataTransfer.effectAllowed = 'move';
  }

  _dragOver(e) {
    e.preventDefault();
  }

  // need to maintain a drag enter counter for the item, as the drag enter/leave event happen when a child
  // node is being moved over and we know only that we left the parent element when the counter reaches 0.
  _dragEnter(e) {
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';

    const itemIndex = Number(e.currentTarget.dataset.itemIndex);
    const item = this.model.items[itemIndex];
    const dragEnterCount = item.dragEnterCount || 0;
    if (dragEnterCount <= 0) {
      item.dragEnterCount = 1;
      e.currentTarget.classList.add('droppable');
    } else {
      item.dragEnterCount = dragEnterCount + 1;
    }
  }

  _dragLeave(e) {
    e.preventDefault();
    const itemIndex = Number(e.currentTarget.dataset.itemIndex);
    const item = this.model.items[itemIndex];

    item.dragEnterCount -= 1;
    if (item.dragEnterCount <= 0) {
      e.currentTarget.classList.remove('droppable');
    }
  }

  _drop(e) {
    e.preventDefault();

    const toIndex = Number(e.currentTarget.dataset.itemIndex);
    this.model.items[toIndex].dragEnterCount = 0;
    e.currentTarget.classList.remove('droppable');

    const fromData = e.dataTransfer.getData('text/plain');
    const fromIndex = Number(fromData);

    // this will trigger a re-render and update the data-item-index attributes
    this.model.moveItem(fromIndex, toIndex);

    // setting the focus on the dropped item should be done when when the data-item-index
    // attributes are set, so we schedule it at the end of the next render cycle
    this.scheduler.add(() => {
      const dropped = this.shadowRoot.querySelector(`[data-item-index="${toIndex}"]`);
      if (dropped) dropped.focus();
    });
  }

  _upClick(e) {
    const itemDiv = e.currentTarget.closest('.list-item');
    const itemIndex = Number(itemDiv.dataset.itemIndex);
    this.model.moveItem(itemIndex, itemIndex - 1);
  }

  _downClick(e) {
    const itemDiv = e.currentTarget.closest('.list-item');
    const itemIndex = Number(itemDiv.dataset.itemIndex);
    this.model.moveItem(itemIndex, itemIndex + 1);
  }

  _itemListKeydown(e) {
    switch (e.key) {
      case 'ArrowDown':
      case 'ArrowRight': {
        const nextSib = e.target.closest('li').nextElementSibling;
        if (nextSib) nextSib.focus();
        break;
      }
      case 'ArrowUp':
      case 'ArrowLeft': {
        const prevSib = e.target.closest('li').previousElementSibling;
        if (prevSib) prevSib.focus();
        break;
      }
      case 'Enter': {
        const itemNode = e.target.closest('[data-item-index]');
        this.model.selectIndex(itemNode.dataset.itemIndex, true);
        break;
      }
      case ' ': {
        const itemNode = e.target.closest('[data-item-index]');
        const checkbox = itemNode.querySelector('input[type="checkbox"]');
        this.model.selectIndex(itemNode.dataset.itemIndex, !checkbox.checked);
        break;
      }
      default:
        // ignore, let bubble up
        return;
    }
    e.preventDefault();
  }

  // NOTE: the checked status of a checkbox may not be properly rendered when the checked attribute is set,
  //       because that applies to inital rendering only. However, setting the checked property works!
  _getCheckBox(model, item) {
    const chkid = `item-chk-${model.getItemId(item)}`;
    return html`
<input type="checkbox" id=${chkid}
  tabindex="-1"
  class="kdsoft-checkbox"
  @click=${this._checkboxClicked}
  .checked=${model.isItemSelected(item)}
  ?disabled=${item.disabled} />
<label for=${chkid}><span>${model.getItemText(item)}</span></label>
`;
  }

  _itemTemplate(item, indx, showCheckboxes, hasArrows, allowDragAndDrop) {
    const disabledString = item.disabled ? 'disabled' : '';
    const tabindex = indx === 0 ? '0' : '-1';
    const upArrowClasses = indx === 0 ? classList.upArrowHidden : classList.upArrowVisible;
    const downArrowClasses = indx >= (this.model.items.length - 1) ? classList.downArrowHidden : classList.downArrowVisible;

    const listItemContent = html`
      <a>
        ${hasArrows
        ? html`
  <span class="leading-normal cursor-pointer" @click=${this._upClick}><i class=${classMap(upArrowClasses)}></i></span>
  <span class="leading-normal cursor-pointer" @click=${this._downClick}><i class=${classMap(downArrowClasses)}></i></span>
  `
        : nothing}
        ${showCheckboxes ? this._getCheckBox(this.model, item, indx) : html`<span>${this.model.getItemText(item)}</span>`}
      </a>
    `;

    let result;

    if (allowDragAndDrop) {
      result = html`
      <li data-item-index="${indx}"
          tabindex="${tabindex}"
          draggable="true"
          class="list-item whitespace-no-wrap ${disabledString}"
          title=${this.model.getItemText(item)}
          @dragstart=${this._dragStart}
          @dragenter=${this._dragEnter}
          @dragover=${this._dragOver}
          @dragleave=${this._dragLeave}
          @drop=${this._drop}
      >
        ${listItemContent}
      </li>
      `;
    } else {
      result = html`
      <li data-item-index="${indx}"
          tabindex="${tabindex}"
          draggable="true"
          class="list-item whitespace-no-wrap ${disabledString}"
          title=${this.model.getItemText(item)}
      >
        ${listItemContent}
      </li>
      `;
    }

    return result;
  }

  // using the repeat directive
  render() {
    const showCheckboxes = this.showCheckboxes;
    const hasArrows = this.arrows;
    const allowDragAndDrop = this.allowDragDrop;

    const result = html`
      <link rel="stylesheet" type="text/css" href=${styleLinks.tailwind} />
      <link rel="stylesheet" type="text/css" href=${styleLinks.fontawesome} />
      <link rel="stylesheet" type="text/css" href=${styleLinks.checkbox} />
      <style>
        :host {
          display: inline-block;
        }
      </style>
      <div id="container" class="border" @click=${this._dropdownClicked}>
        <ul id="item-list"
          class="bg-white border-solid border border-gray-400 overflow-y-auto"
          @keydown=${this._itemListKeydown}
          @click=${this._itemListClick}
        >
          ${repeat(this.model.filteredItems, entry => this.model.getItemId(entry.item), entry => this._itemTemplate(entry.item, entry.index, showCheckboxes, hasArrows, allowDragAndDrop))}
        </ul>
      </div>
    `;
    return result;
  }

  initView() {
    if (!this.model) return;

    let firstSelEntry = null;
    for (const selEntry of this.model.selectedEntries) {
      firstSelEntry = selEntry;
      break;
    }
    if (firstSelEntry) {
      const firstSelected = this.shadowRoot.querySelector(`.list-item[data-item-index="${firstSelEntry.index}"]`);
      //firstSelected.scrollIntoView(true); --  does not work in ShadowDom
      const op = firstSelected.offsetParent;
      if (op) op.scrollTop = firstSelected.offsetTop;
    }
  }
}

window.customElements.define('kdsoft-checklist', KdSoftCheckList);
