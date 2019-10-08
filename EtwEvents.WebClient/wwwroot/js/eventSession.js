import { observable } from '../lib/@nx-js/observer-util.js';

class EventSession {
  constructor(wsUrl, bufferSize) {
    this.wsUrl = wsUrl;
    this.ws = null;
    this._open = false;
    this._error = '';
    this._items = new Array(bufferSize);
    this._itemOffset = 0;
    return observable(this);
  }

  get open() { return this._open; }
  get error() { return this._error; }

  _addItem(item) {
    let itemIndex = this._itemOffset;
    this._items[itemIndex] = item;
    itemIndex += 1;
    if (itemIndex >= this._items.length) itemIndex -= this._items.length;
    this._itemOffset = itemIndex;
  }

  // iterate over ringbuffer
  itemIterator() {
    const self = this;

    return {
      [Symbol.iterator]() {
        this.current = 0;
        return this;
      },

      next() {
        if (this.current >= 0 && this.current < self._items.length) {
          for (let index = this.current; index < self._items.length; index += 1) {
            let itemIndex = self._itemOffset + index;
            if (itemIndex >= self._items.length) itemIndex -= self._items.length;
            const item = self._items[itemIndex];
            this.current += 1;
            if (item) {
              return { done: false, value: item };
            }
          }
          //let itemIndex = self._itemOffset + this.current;
          //if (itemIndex >= self._items.length) itemIndex -= self._items.length;
          //const item = self._items[itemIndex];
          //this.current += 1;
          //return { done: false, value: item };
        }
        return { done: true };
      },
    };
  }

  connect() {
    if (this.ws !== null) return;

    this.ws = new WebSocket(this.wsUrl);

    this.ws.onclose = (e) => {
      this._open = false;
      this.ws = null;
    };

    this.ws.onopen = (e) => {
      this._open = true;
    };

    this.ws.onerror = (e) => {
      this._error = 'Web socket error';
    };

    this.ws.onmessage = (ev) => {
      let logEvent;
      try {
        logEvent = JSON.parse(ev.data);
        this._addItem(logEvent);
      } catch (e) {
        console.log(ev.data);
        console.log(e);
      }
    };
  }

  disconnect() {
    if (this.ws === null) return;
    this.ws.close();
    this.ws = null;
  }
}

export default EventSession;
