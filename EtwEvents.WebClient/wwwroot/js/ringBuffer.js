import { observable } from '../lib/@nx-js/observer-util.js';

// Undefined entries will be skipped on iteration
class RingBuffer {
  constructor(bufferSize) {
    this._items = new Array(bufferSize);
    this._itemOffset = 0;
    return observable(this);
  }

  addItem(item) {
    let itemIndex = this._itemOffset;
    this._items[itemIndex] = item;
    itemIndex += 1;
    if (itemIndex >= this._items.length) itemIndex = 0;
    this._itemOffset = itemIndex;
  }

  addItems(newItems) {
    const delta = newItems.length - this._items.length;
    if (delta >= 0) {
      this._items = newItems.slice(delta);
      this._itemOffset = 0;
    } else {
      let newOffset = this._itemOffset + newItems.length;
      const overshoot = newOffset - this._items.length;

      if (overshoot >= 0) {
        const chunk1Length = newItems.length - overshoot;
        this._items.splice(this._itemOffset, chunk1Length, newItems.slice(0, chunk1Length));
        this._items.splice(0, overshoot, ...newItems.slice(chunk1Length));
        newOffset = overshoot;
      } else {
        this._items.splice(this._itemOffset, newItems.length, ...newItems);
      }

      this._itemOffset = newOffset;
    }
  }

  // iterate over ringbuffer
  itemIterator() {
    const self = this;

    return {
      [Symbol.iterator]() {
        this.current = self._itemOffset;
        this.limit = self._itemOffset === 0 ? self._items.length - 1 : self._itemOffset - 1;
        return this;
      },

      next() {
        while (this.current !== this.limit) {
          let itemIndex = this.current;
          const item = self._items[itemIndex];
          itemIndex += 1;
          if (itemIndex >= self._items.length) itemIndex = 0;
          this.current = itemIndex;

          if (typeof item !== 'undefined') {
            return { done: false, value: item };
          }
        }
        return { done: true };
      }
    };
  }
}

export default RingBuffer;
