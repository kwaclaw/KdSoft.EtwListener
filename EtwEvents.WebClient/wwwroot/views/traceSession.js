import { observable } from '../lib/@nx-js/observer-util.js';
import EventSession from '../js/eventSession.js';

class TraceSession {
  constructor(profile) {
    this._profile = profile;
    this.providers = profile.providers.slice(0);
    this.filter = profile.activeFilter;
    this._enabledProviders = [];
    this._restartedProviders = [];
    this._eventSession = null;
    this._open = false;
    return observable(this);
  }

  get profile() { return this._profile; }
  get enabledProviders() { return this._enabledProviders; }
  get restartedProviders() { return this._restartedProviders; }

  get open() { return this._open; }

  get eventSession() { return this._eventSession; }

  async closeRemoteSession() {
    try {
      const response = await fetch(`/Etw/CloseRemoteSession?name=${this._profile.name}`, {
        method: 'POST', // or 'PUT'
        headers: { 'Content-Type': 'application/json' }
      });

      if (response.ok) {
        const jobj = await response.json();
        this._open = false;
        console.log('Success:', JSON.stringify(jobj));
      } else {
        console.log('Error:', await response.text());
      }
    } catch (error) {
      console.error('Error:', error);
    }
  }

  async openSession() {
    const p = this._profile;
    const request = { name: p.name, host: p.host, providers: this.providers, lifeTime: p.lifeTime };
    const requestJson = JSON.stringify(request);

    try {
      const response = await fetch('/Etw/OpenSession', {
        method: 'POST', // or 'PUT'
        body: requestJson, // data can be `string` or {object}!
        headers: { 'Content-Type': 'application/json' }
      });

      if (response.ok) {
        const jobj = await response.json();
        this._enabledProviders = jobj.enabledProviders;
        this._restartedProviders = jobj.restartedProviders;
        this._open = true;
        console.log('Success:', JSON.stringify(jobj));
        return true;
      } else {
        console.log('Error:', await response.text());
      }
    } catch (error) {
      console.error('Error:', error);
    }
    return false;
  }

  toggleEvents() {
    let evs = this._eventSession;
    if (!evs) {
      // scroll bug in Chrome - will not show more than about 1000 items, works fine with FireFox
      evs = new EventSession(`wss://${window.location.host}/Etw/StartEvents?sessionName=${this.profile.name}`, 900);
      this._eventSession = evs;
    }
    if (!evs.open) {
      evs.connect();
    } else {
      evs.disconnect();
    }
  }

  async _callFilter(method, data) {
    const url = new URL(`/Etw/${method}`, window.location);
    try {
      const response = await fetch(url, {
        method: 'POST',
        body: JSON.stringify(data),
        headers: { 'Content-Type': 'application/json' }
      });

      if (response.ok) {
        const jobj = await response.json();
        console.log('Success:', JSON.stringify(jobj));
        return { success: true, details: jobj };
      }
      
      const msg = await response.text();
      console.log('Error:', msg);
      return { success: false, details: msg };
    } catch (error) {
      console.error('Error:', error);
      throw error;
    }
  }

  applyFilter(filter) {
    return this._callFilter('SetCSharpFilter', { sessionName: this.profile.name, csharpFilter: filter || null });
  }

  testFilter(filter) {
    return this._callFilter('TestCSharpFilter', { host: this.profile.host, csharpFilter: filter || null });
  }
}

export default TraceSession;
