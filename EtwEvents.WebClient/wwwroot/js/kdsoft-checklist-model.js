
import { observable, raw } from '../lib/@nx-js/observer-util.js';
import { html } from '../lib/lit-html.js';

function iterateSelectedItems(items, selectedItems) {
  let current;

  return {
    [Symbol.iterator]() {
      current = 0;
      return this;
    },

    next() {
      while (current >= 0 && current < items.length) {
        const item = items[current];
        const index = current;
        current += 1;
        if (selectedItems.has(raw(item))) {
          return { done: false, value: { item, index } };
        }
        continue;
      }
      return { done: true };
    },
  };
}

function iterateFilter(items, filter) {
  let current;

  return {
    [Symbol.iterator]() {
      current = 0;
      return this;
    },

    next() {
      while (current >= 0 && current < items.length) {
        const item = items[current];
        const index = current;
        current += 1;
        if (item.selected || (!filter || filter(item))) return { done: false, value: { item, index } };
        continue;
      }
      return { done: true };
    },
  };
}


const _multiSelect = new WeakMap();

class KdSoftCheckListModel {
  constructor(
    items = [],
    selectedIndexes = [],
    multiSelect = true,
    getItemTemplate = item => html``,
    getItemId = item => item.id
  ) {
    if (!multiSelect && (selectedIndexes || []).length > 1) {
      throw new Error('Must not select multiple items');
    }

    this.items = items;
    this.filter = null;
    this._selectedItems = new WeakSet((selectedIndexes || []).map(i => raw(items[i])));

    const result = observable(this);

    // so that we can use this in the property getters/setters
    _multiSelect.set(result, multiSelect);

    this.getItemTemplate = getItemTemplate;
    this.getItemId = getItemId;

    return result;
  }

  get multiSelect() { return _multiSelect.get(this); }

  get selectedEntries() { return iterateSelectedItems(this.items, this._selectedItems); }

  get selectedIndexes() {
    const result = [];
    for (const selEntry of this.selectedEntries) {
      result.push(selEntry.index);
    }
    return result;
  }

  get filteredItems() { return iterateFilter(this.items, this.filter); }

  selectIndex(index, select) {
    if (this.multiSelect) {
      if (select) this._selectedItems.add(raw(this.items[index]));
      else this._selectedItems.delete(raw(this.items[index]));
    } else if (select) {
      this._selectedItems = new WeakSet([raw(this.items[index])]);
    } else {
      this._selectedItems = new WeakSet();
    }
  }

  toggleIndex(index) {
    const rawItem = raw(this.items[index]);
    const isSelected = this._selectedItems.has(rawItem);
    if (this.multiSelect) {
      if (isSelected) this._selectedItems.delete(rawItem);
      else this._selectedItems.add(rawItem);
    } else if (isSelected) {
      this._selectedItems = new WeakSet();
    } else {
      this._selectedItems = new WeakSet(rawItem);
    }
  }

  isItemSelected(item) {
    return this._selectedItems.has(raw(item));
  }

  // expects numbers
  moveItem(from, to) {
    if (from === to) return;

    // this algorithm keeps the array length constant
    const itemToMove = this.items[from];
    if (to > from) {
      this.items.copyWithin(from, from + 1, to + 1);
    } else if (to < from) {
      this.items.copyWithin(to + 1, to, from);
    }
    this.items[to] = itemToMove;
  }

  unselectAll() {
    this._selectedItems = new WeakSet();
  }
}

export default KdSoftCheckListModel;
