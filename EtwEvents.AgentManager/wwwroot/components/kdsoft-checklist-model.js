
import { observable, raw } from '../lib/@nx-js/observer-util/dist/es.es6.js';

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

// selected items are always included
function iterateFilter(items, selectedItems, filter) {
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
        if (selectedItems.has(raw(item)) || (!filter || filter(item))) {
          return { done: false, value: { item, index } };
        }
        continue;
      }
      return { done: true };
    },
  };
}

const _multiSelect = new WeakMap();

class KdSoftChecklistModel {
  constructor(
    items = [],
    selectedIndexes = [],
    multiSelect = true,
    getItemId = item => item.id
  ) {
    if (!multiSelect && (selectedIndexes || []).length > 1) {
      throw new Error('Must not select multiple items');
    }

    this.items = items;
    this.filter = null;
    const selItems = (selectedIndexes || []).map(i => raw(items[i]));
    this._selectedItems = new WeakSet(selItems);

    const result = observable(this);

    // so that we can use this in the property getters/setters
    _multiSelect.set(result, multiSelect);

    this.getItemId = getItemId;

    return result;
  }

  get multiSelect() { return _multiSelect.get(this); }

  get selectedEntries() { return iterateSelectedItems(this.items, this._selectedItems); }

  get firstSelectedEntry() {
    return this.selectedEntries[Symbol.iterator]().next().value;
  }

  get selectedIndexes() {
    return Array.from(this.selectedEntries, entry => entry.index);
  }

  get selectedItems() {
    return Array.from(this.selectedEntries, entry => entry.item);
  }

  get filteredItems() { return iterateFilter(this.items, this._selectedItems, this.filter); }

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

  selectId(id, select) {
    let selItem = null;
    for (let indx = 0; indx < this.items.length; indx += 1) {
      const tempItem = this.items[indx];
      if (this.getItemId(tempItem) === id) {
        selItem = tempItem;
        break;
      }
    }
    if (selItem === null) return;

    if (this.multiSelect) {
      if (select) this._selectedItems.add(raw(selItem));
      else this._selectedItems.delete(raw(selItem));
    } else if (select) {
      this._selectedItems = new WeakSet([raw(selItem)]);
    } else {
      this._selectedItems = new WeakSet();
    }
  }

  selectIds(ids, select) {
    if (!this.multiSelect && (ids || []).length > 1) {
      throw new Error('Must not select multiple items');
    }

    const selItems = [];
    for (let indx = 0; indx < this.items.length; indx += 1) {
      const tempItem = this.items[indx];
      for (const id of ids) {
        if (this.getItemId(tempItem) === id) {
          selItems.push(tempItem);
          break;
        }
      }
    }

    for (let indx = 0; indx < selItems.length; indx += 1) {
      const selItem = raw(selItems[indx]);
      if (this.multiSelect) {
        if (select) this._selectedItems.add(selItem);
        else this._selectedItems.delete(selItem);
      } else if (select) {
        this._selectedItems = new WeakSet([selItem]);
      } else {
        this._selectedItems = new WeakSet();
      }
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

    const items = raw(this.items);

    // this algorithm keeps the array length constant
    const itemToMove = items[from];
    if (to > from) {
      items.copyWithin(from, from + 1, to + 1);
    } else if (to < from) {
      items.copyWithin(to + 1, to, from);
    }
    items[to] = itemToMove;

    // we made changes on the raw array, because copyWithin and assignments applied to the proxy
    // 'this.items' strip the copied/assigned array elements of any proxies that might wrap them.
    // So we need to trigger a reaction explicity on this.items by cloning the items andre-assigning the property.
    // Simply re-assigning will not trigger a reaction, as the raw itmes object would not have changed.
    // Clearing and re-assigning will trigger a reaction, but will break code that relies on the items
    // property not changing in size and array elements, but only in their order.
    this.items = items.slice();
  }

  unselectAll() {
    this._selectedItems = new WeakSet();
  }
}

export default KdSoftChecklistModel;
