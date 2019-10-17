import { observable } from '../lib/@nx-js/observer-util.js';
import RingBuffer from './ringBuffer.js';

class EventSession {
  constructor(wsUrl, bufferSize) {
    this.wsUrl = wsUrl;
    this.ws = null;
    this._open = false;
    this._error = '';
    this._buffer = new RingBuffer(bufferSize);
    return observable(this);
  }

  get open() { return this._open; }
  get error() { return this._error; }

  itemIterator() { return this._buffer.itemIterator(); }

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
        this._buffer.addItems(logEvent);
      } catch (e) {
        //console.log(ev.data);
        console.log(e);
      }
    };
  }

  disconnect() {
    if (this.ws === null) return;
    this.ws.close();
    this._open = false;
    this.ws = null;
  }
}

export default EventSession;
