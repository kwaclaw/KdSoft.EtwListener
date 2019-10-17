import { observable } from '../lib/@nx-js/observer-util.js';
import EventSession from './eventSession.js';

class TraceSession {
  constructor(profile) {
    this._profile = profile;
    this.providers = profile.providers.slice(0);
    this.filter = profile.filter;
    this._enabledProviders = [];
    this._failedProviders = [];
    this._eventSession = null;
    this._open = false;
    return observable(this);
  }

  get profile() { return this._profile; }
  get enabledProviders() { return this._enabledProviders; }
  get failedProviders() { return this._failedProviders; }

  get open() { return this._open; }

  get eventSession() { return this._eventSession; }

  async closeRemoteSession() {
    try {
      const response = await fetch(`/Etw/CloseRemoteSession?name=${this._profile.name}`, {
        method: 'POST', // or 'PUT'
        headers: { 'Content-Type': 'application/json' }
      });

      const jobj = await response.json();
      if (response.ok) {
        this._open = false;
        console.log('Success:', JSON.stringify(jobj));
      } else {
        this._open = false;
        console.log('Error:', JSON.stringify(jobj));
      }
    } catch (error) {
      console.error('Error:', error);
    }
  }

  async openSession() {
    const p = this._profile;
    const request = { name: p.name, host: p.host, providers: this.providers, lifeTime: p.lifeTime };

    try {
      const response = await fetch('/Etw/OpenSession', {
        method: 'POST', // or 'PUT'
        body: JSON.stringify(request), // data can be `string` or {object}!
        headers: { 'Content-Type': 'application/json' }
      });

      const jobj = await response.json();
      if (response.ok) {
        this._enabledProviders = jobj.enabledProviders;
        this._failedProviders = jobj.failedProviders;
        this._open = true;
        console.log('Success:', JSON.stringify(jobj));
      }  else {
        console.log('Error:', JSON.stringify(jobj));
      }
    } catch (error) {
      console.error('Error:', error);
    }
  }

  toggleEvents() {
    let evs = this._eventSession;
    if (!evs) {
      // scroll bug in Chrome - will not show more than about 1000 items, works fine with FireFox
      evs = new EventSession(`ws://${window.location.host}/Etw/StartEvents?sessionName=${this.profile.name}`, 900);
      this._eventSession = evs;
    }
    if (!evs.open) {
      evs.connect();
    } else {
      evs.disconnect();
    }
  }
}

export default TraceSession;
