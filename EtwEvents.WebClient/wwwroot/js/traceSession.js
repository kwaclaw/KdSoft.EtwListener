import { observable } from '../lib/@nx-js/observer-util.js';
import EventSession from './eventSession.js';

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

      const jobj = await response.json();
      const status = jobj.status || 200;
      if (response.ok && (status >= 200 && status < 300)) {
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
    const requestJson = JSON.stringify(request);

    try {
      const response = await fetch('/Etw/OpenSession', {
        method: 'POST', // or 'PUT'
        body: requestJson, // data can be `string` or {object}!
        headers: { 'Content-Type': 'application/json' }
      });

      const jobj = await response.json();
      const status = jobj.status || 200;
      if (response.ok && (status >= 200 && status < 300)) {
        this._enabledProviders = jobj.enabledProviders;
        this._restartedProviders = jobj.restartedProviders;
        this._open = true;
        console.log('Success:', JSON.stringify(jobj));
        return true;
      }
      console.log('Error:', JSON.stringify(jobj));
    } catch (error) {
      console.error('Error:', error);
    }
    return false;
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

  async applyFilter(newFilter) {
    const url = new URL('/Etw/SetCSharpFilter', window.location);
    // url.searchParams.set('sessionName', this.profile.name);
    // url.searchParams.set('csharpFilter', newFilter);

    const data = { sessionName: this.profile.name, csharpFilter: newFilter || null };
    try {
      const response = await fetch(url, {
        method: 'POST',
        body: JSON.stringify(data),
        headers: { 'Content-Type': 'application/json' }
      });

      const jobj = await response.json();
      const status = jobj.status || 200;
      if (response.ok && (status >= 200 && status < 300)) {
        console.log('Success:', JSON.stringify(jobj));
        return { success: true, details: jobj };
      }
      console.log('Error:', JSON.stringify(jobj));
      return { success: false, details: jobj };
    } catch (error) {
      console.error('Error:', error);
      throw error;
    }
  }
}

export default TraceSession;
