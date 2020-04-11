import { observable, observe } from '../lib/@nx-js/observer-util.js';
import EventSession from './eventSession.js';
import FetchHelper from './fetchHelper.js';

class TraceSession {
  constructor(profile, state) {
    this._profile = profile;
    this._state = state || {};
    this.filter = profile.activeFilter;
    this._eventSession = null;
    this.fetcher = new FetchHelper('/Etw');
    return observable(this);
  }

  get name() { return this._state ? this._state.name : (this._profile ? this._profile.name : '<unknown>'); }
  get profile() { return this._profile; }
  get state() { return this._state; }
  get eventSession() { return this._eventSession; }

  updateState(newState) {
    this._state = newState;
  }

  async closeRemoteSession(progress) {
    try {
      await this.fetcher.withProgress(progress).post('CloseRemoteSession', { name: this._profile.name });
      //console.log(response);
    } catch (error) {
      window.myapp.defaultHandleError(error);
    }
  }

  async openSession(progress) {
    const p = this._profile;
    const request = { name: p.name, host: p.host, providers: p.providers, lifeTime: p.lifeTime };

    try {
      const response = await this.fetcher.withProgress(progress).postJson('OpenSession', null, request);
      this._state = response;
      this._restartedProviders = response.restartedProviders;
      return true;
    } catch (error) {
      window.myapp.defaultHandleError(error);
    }

    return false;
  }

  async toggleEvents(progress) {
    if (this.state.isRunning) {
      try {
        await this.fetcher.withProgress(progress).post('StopEvents', { sessionName: this._profile.name });
      } catch (error) {
        window.myapp.defaultHandleError(error);
      }
    } else {
      try {
        await this.fetcher.withProgress(progress).get('StartEvents', { sessionName: this._profile.name });
      } catch (error) {
        window.myapp.defaultHandleError(error);
      }
    }
  }

  observeEvents() {
    let evs = this._eventSession;
    if (evs && evs.open) return;

    if (evs) {
      evs.disconnect();
    }

    // scroll bug in Chrome - will not show more than about 1000 items, works fine with FireFox
    evs = new EventSession(
      `wss://${window.location.host}/Etw/ObserveEvents?sessionName=${this.profile.name}`,
      900,
      error => window.myapp.defaultHandleError(error)
    );
    evs.connect();
    observe(() => {
      if (evs.open) this._eventSession = evs;
      else this._eventSession = null;
    });
  }

  unobserveEvents() {
    const evs = this._eventSession;
    if (evs) {
      evs.disconnect();
      this._eventSession = null;
    }
  }

  async _callFilter(method, data, progress) {
    //const url = new URL(`/Etw/${method}`, window.location);
    try {
      const response = await this.fetcher.withProgress(progress).postJson(method, null, data);
      return { success: true, details: response };
    } catch (error) {
      console.error(error);
      return { success: false, error };
    }
  }

  applyFilter(filter, progress) {
    return this._callFilter('SetCSharpFilter', { sessionName: this.profile.name, csharpFilter: filter || null }, progress);
  }

  testFilter(filter, progress) {
    return this._callFilter('TestCSharpFilter', { host: this.profile.host, csharpFilter: filter || null }, progress);
  }
}

export default TraceSession;
