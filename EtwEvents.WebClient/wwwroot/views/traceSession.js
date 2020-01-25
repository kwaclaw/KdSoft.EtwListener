﻿import { observable } from '../lib/@nx-js/observer-util.js';
import EventSession from '../js/eventSession.js';
import FetchHelper from '../js/fetchHelper.js';

class TraceSession {
  constructor(profile) {
    this._profile = profile;
    this.providers = profile.providers.slice(0);
    this.filter = profile.activeFilter;
    this._enabledProviders = [];
    this._restartedProviders = [];
    this._eventSession = null;
    this._open = false;
    this.fetcher = new FetchHelper('/Etw');
    return observable(this);
  }

  get profile() { return this._profile; }
  get enabledProviders() { return this._enabledProviders; }
  get restartedProviders() { return this._restartedProviders; }

  get open() { return this._open; }

  get eventSession() { return this._eventSession; }

  async closeRemoteSession(progress) {
    try {
      await this.fetcher.withProgress(progress)
        .post('CloseRemoteSession', { name: this._profile.name });
      this._open = false;
      //console.log(response);
    } catch (error) {
      window.myapp.defaultHandleError(error);
    }
  }

  async openSession(progress) {
    const p = this._profile;
    const request = { name: p.name, host: p.host, providers: this.providers, lifeTime: p.lifeTime };

    try {
      const response = await this.fetcher.withProgress(progress).postJson('OpenSession', null, request);
      this._enabledProviders = response.enabledProviders;
      this._restartedProviders = response.restartedProviders;
      this._open = true;
      return true;
    } catch (error) {
      window.myapp.defaultHandleError(error);
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