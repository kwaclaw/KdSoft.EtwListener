import { observable } from '../lib/@nx-js/observer-util.js';
import RingBuffer from './ringBuffer.js';

const SessionNotFoundCode = 4901;
const NormalClosureCode = 1000;

class EventSession {
  constructor(wsUrl, bufferSize, handleError) {
    this.wsUrl = wsUrl;
    this.ws = null;
    this._openCount = 0;
    this._handleError = handleError;
    this._buffer = new RingBuffer(bufferSize);
    return observable(this);
  }

  get open() { return this._openCount > 0; }

  itemIterator() { return this._buffer.itemIterator(); }

  connect() {
    if (this.ws !== null) return;

    this.ws = new WebSocket(this.wsUrl);

    this.ws.onclose = (e) => {
      this.ws = null;
      this._openCount -= 1;
      if (e.code > NormalClosureCode) {
        if (e.code == 1006)  // not of interest to user
          console.log(e);
        else {
          const error = { title: e.reason || 'websocket error', statusCode: e.code };
          this._handleError(error);
        }
      }
    };

    this.ws.onopen = (e) => {
      this._openCount += 1;
    };

    this.ws.onerror = (errorEvent) => {
      let error;
      if (errorEvent && errorEvent instanceof Error) {
        error = { title: errorEvent.message || 'WebSocket error' };
      } else if (!errorEvent || typeof errorEvent !== 'object') {
        error = { title: errorEvent || 'WebSocket error' };
      } else {
        error = {
          title: errorEvent.message || 'WebSocket error'
        };
      }
      this._handleError(error);
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
    this.ws.close(NormalClosureCode);
    this.ws = null;
  }
}

export default EventSession;
