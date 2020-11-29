import { observable, observe, unobserve, raw } from '../lib/@nx-js/observer-util/dist/es.es6.js';
import TraceSession from '../js/traceSession.js';
import TraceSessionProfile from '../js/traceSessionProfile.js';
import EventSinkProfile from '../js/eventSinkProfile.js';
import * as utils from '../js/utils.js';
import RingBuffer from '../js/ringBuffer.js';
import FetchHelper from '../js/fetchHelper.js';

function loadSessionProfiles() {
  const sessionProfiles = [];
  for (let i = 0; i < localStorage.length; i += 1) {
    const key = localStorage.key(i);
    if (key.startsWith('session-profile-')) {
      const item = JSON.parse(localStorage.getItem(key));
      const profile = new TraceSessionProfile();
      utils.setTargetProperties(profile, item);
      sessionProfiles.push(profile);
    }
  }
  sessionProfiles.sort((x, y) => String.prototype.localeCompare.call(x.name, y.name));
  return sessionProfiles;
}

function loadEventSinkProfiles() {
  const sinkProfiles = [];
  for (let i = 0; i < localStorage.length; i += 1) {
    const key = localStorage.key(i);
    if (key.startsWith('sink-profile-')) {
      const item = JSON.parse(localStorage.getItem(key));
      const sinkProfile = new EventSinkProfile();
      utils.setTargetProperties(sinkProfile, item);
      sinkProfiles.push(sinkProfile);
    }
  }
  sinkProfiles.sort((x, y) => String.prototype.localeCompare.call(x.name, y.name));
  return sinkProfiles;
}


function getSessionProfileFromState(state) {
  return new TraceSessionProfile(state.name, state.host, state.enabledProviders);
}

class EtwAppModel {
  constructor() {
    this._traceSessions = observable(new Map());
    this._visibleSessionNames = observable(new Set());
    this._visibleSessionsObserver = null;
    this.activeSessionName = null;

    this.sessionProfiles = loadSessionProfiles();

    this.eventSinkProfiles = loadEventSinkProfiles();

    this._errorSequenceNo = 0;
    this.fetchErrors = new RingBuffer(50);
    this.showLastError = false;
    this.showErrors = false;

    // this._openSessions = [];
    this.fetcher = new FetchHelper('/Etw');
    this.fetcher.getJson('GetSessionStates')
      .then(st => this._updateTraceSessions(st.sessions))
      .catch(error => window.etwApp.defaultHandleError(error));

    const es = new EventSource('Etw/GetSessionStates');
    es.onmessage = e => {
      console.log(e.data);
      const st = JSON.parse(e.data);
      this._updateTraceSessions(st.sessions);
    };
    es.onerror = err => {
      console.error(err);
    };

    return observable(this);
  }

  handleFetchError(error) {
    this._errorSequenceNo += 1;
    if (!error.timeStamp) error.timeStamp = new Date();
    error.sequenceNo = this._errorSequenceNo;

    this.fetchErrors.addItem(error);
    this.showLastError = true;
    if (this._errorTimeout) window.clearTimeout(this._errorTimeout);
    this._errorTimeout = window.setTimeout(() => { this.showLastError = false; }, 9000);
  }

  keepErrorsOpen() {
    if (this._errorTimeout) {
      window.clearTimeout(this._errorTimeout);
    }
  }

  //#region TraceSession

  get traceSessions() { return this._traceSessions; }
  get activeSession() { return this._traceSessions.get(this.activeSessionName); }

  get visibleSessions() {
    const result = [];
    this._visibleSessionNames.forEach(sessionName => {
      const session = this._traceSessions.get(sessionName);
      if (session) result.push(session);
    });
    return result;
  }

  observeVisibleSessions() {
    this._visibleSessionsObserver = observe(() => {
      // we don't want changes to this.activeSessionName trigger a call to this observer
      const activeSessionName = raw(this).activeSessionName;
      const traceSessionList = [...this.traceSessions.values()];
      const visibleSessions = new Set();

      traceSessionList.forEach(ts => {
        if (ts.eventSession) {
          visibleSessions.add(ts.name.toLowerCase());
        }
      });

      // find they key that was inserted before (or after) the current key
      if (!visibleSessions.has(activeSessionName)) {
        const prevKey = utils.closest(this._visibleSessionNames, activeSessionName);
        if (visibleSessions.has(prevKey)) {
          this.activeSessionName = prevKey;
        } else {
          this.activeSessionName = visibleSessions.values().next().value;
        }
      }

      this._visibleSessionNames = visibleSessions;
    });
  }

  unobserveVisibleSessions() {
    if (this._visibleSessionsObserver) {
      unobserve(this._visibleSessionsObserver);
      this._visibleSessionsObserver = null;
    }
  }

  watchSession(session) {
    // _visibleSessionsObserver will be called multiple times, we want to use the same session name
    // for the callback across these calls to _visibleSessionsObserver
    const sessionName = session.name.toLowerCase();
    session.observeEvents(eventSession => {
      if (eventSession) this.activeSessionName = sessionName;
    });
  }

  unwatchSession(session) {
    session.unobserveEvents();
  }

  activateSession(sessionName) {
    this.activeSessionName = sessionName.toLowerCase();
  }

  closeSession(session, progress) {
    session.closeRemoteSession(progress);
  }

  //#endregion TraceSession

  //#region TraceSessionProfile

  _updateTraceSessions(sessionStates) {
    const localSessionKeys = new Set(this.traceSessions.keys());
    const profiles = this.sessionProfiles;

    // sessionStates have unique names (case-insensitive) - //TODO server culture vs local culture?
    for (const state of (sessionStates || [])) {
      const profileIndex = profiles.findIndex(p => String.prototype.localeCompare.call(p.name, state.name) === 0);
      const sessionName = state.name.toLowerCase();
      let traceSession = this.traceSessions.get(sessionName);

      if (traceSession) {
        traceSession.updateState(state);
      } else {
        const profile = profileIndex >= 0 ? profiles[profileIndex] : getSessionProfileFromState(state);
        if (profileIndex < 0) this.saveProfile(profile);
        traceSession = new TraceSession(profile, state);
        this.traceSessions.set(sessionName, traceSession);
      }

      localSessionKeys.delete(sessionName);
    }

    // remove sessions not present on the server
    for (const sessionKey of localSessionKeys) {
      this.traceSessions.delete(sessionKey);
    }
  }

  async openSessionFromProfile(profile, progress) {
    const sessionName = profile.name.toLowerCase();
    const ses = this.traceSessions.get(sessionName);
    if (ses) return;

    const newSession = new TraceSession(profile);
    const success = await newSession.openSession(progress);
    if (!success) return;

    const filter = newSession.profile.activeFilter;
    if (filter) {
      const result = await newSession.applyFilter(filter, progress);
      if (result.success) {
        if (result.details.diagnostics.length > 0) {
          //TODO show the error somehow
          console.log(JSON.stringify(result.details.diagnostics));
        }
      }
    }
  }

  saveProfile(profileModel) {
    let profileToSave;
    if (profileModel instanceof TraceSessionProfile) {
      profileToSave = profileModel;
    } else {
      profileToSave = new TraceSessionProfile();
      // save profile with only the properties that are defined in the class
      utils.setTargetProperties(profileToSave, profileModel);
    }
    localStorage.setItem(`session-profile-${profileToSave.name.toLowerCase()}`, JSON.stringify(profileToSave));
    this.sessionProfiles = loadSessionProfiles();
  }

  deleteProfile(profileName) {
    localStorage.removeItem(`session-profile-${profileName}`);
    this.sessionProfiles = loadSessionProfiles();
  }

  // should create TraceSessions with a profile
  async importProfiles(files) {
    const promises = [];

    for (let i = 0; i < files.length; i += 1) {
      const file = files[i];
      promises.push(file.text());
    }

    const jsonResults = await Promise.all(promises);
    for (let i = 0; i < jsonResults.length; i += 1) {
      try {
        const profile = JSON.parse(jsonResults[i]);
        this.saveProfile(profile);
      } catch (err) {
        console.log(err);
      }
    }
  }

  //#endregion TraceSessionProfile

  //#region EventSinkProfile

  saveSinkProfile(sinkConfigModel) {
    const sinkProfileToSave = new EventSinkProfile(sinkConfigModel.name, sinkConfigModel.type);
    sinkProfileToSave.definition = sinkConfigModel.definition;
    localStorage.setItem(`sink-profile-${sinkConfigModel.name.toLowerCase()}`, JSON.stringify(sinkProfileToSave));
    this.eventSinkProfiles = loadEventSinkProfiles();
  }

  deleteSinkProfile(sinkProfileName) {
    localStorage.removeItem(`sink-profile-${sinkProfileName.toLowerCase()}`);
    this.eventSinkProfiles = loadEventSinkProfiles();
  }

  //#endregion EventSink Definition
}

export default EtwAppModel;
