import { observable } from '../lib/@nx-js/observer-util.js';

// Undefined entries will be skipped on iteration
class RingBuffer {
  constructor(bufferSize) {
    this._items = new Array(bufferSize);
    // next index to write to, one past the last item, also first item when buffer is full
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
        const oldOffset = this._itemOffset;
        for (let indx = 0; indx < chunk1Length; indx += 1) {
          this._items[indx + oldOffset] = newItems[indx];
        }
        for (let indx = 0; indx < overshoot; indx += 1) {
          this._items[indx] = newItems[indx + chunk1Length];
        }
        newOffset = overshoot;
      } else {
        const oldOffset = this._itemOffset;
        for (let indx = 0; indx < newItems.length; indx += 1) {
          this._items[indx + oldOffset] = newItems[indx];
        }
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
        this.limit = self._itemOffset;
        return this;
      },

      next() {
        let itemIndex = this.current;
        for (let indx = 0; indx < self._items.length; indx += 1) {
          const item = self._items[itemIndex];

          itemIndex += 1;
          if (itemIndex >= self._items.length) itemIndex = 0;

          if (itemIndex === this.limit) {
            break;
          }

          if (typeof item !== 'undefined') {
            this.current = itemIndex;
            return { done: false, value: item };
          }
        }
        return { done: true };
      }
    };
  }
}

export default RingBuffer;
