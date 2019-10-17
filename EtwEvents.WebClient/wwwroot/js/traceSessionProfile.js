import { observable } from '../lib/@nx-js/observer-util.js';

class TraceSessionProfile {
  constructor(name, host, providers, filter, lifeTime) {
    this._name = name;
    this._host = host;
    this._providers = providers;
    this._filter = filter;
    this._lifeTime = lifeTime;
  }
  get name() { return this._name; }
  get host() { return this._host; }
  get providers() { return this._providers; }
  get filter() { return this._filter; }
  get lifeTime() { return this._lifeTime; }
}

export default TraceSessionProfile;
