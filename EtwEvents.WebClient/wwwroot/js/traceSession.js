import { observable } from '../lib/@nx-js/observer-util.js';

class TraceSession {
  constructor(name, enabledProviders, failedProviders) {
    this._name = name;
    this._enabledProviders = enabledProviders;
    this._failedProviders = failedProviders;
    this._eventSession = null;
    return observable(this);
  }

  get name() { return this._name; }
  get enabledProviders() { return this._enabledProviders; }
  get failedProviders() { return this._failedProviders; }

  get eventSession() { return this._eventSession; }
  set eventSession(val) { this._eventSession = val; }
}

export default TraceSession;
