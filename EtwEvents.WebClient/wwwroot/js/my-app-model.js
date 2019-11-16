import { html } from '../lib/lit-html.js';
import { observable } from '../lib/@nx-js/observer-util.js';
import KdSoftCheckListModel from './kdsoft-checklist-model.js';
import KdSoftDropdownModel from './kdsoft-dropdown-model.js';
import TraceSession from './traceSession.js';
import TraceSessionProfile from './traceSessionProfile.js';
import * as utils from './utils.js';
import TraceSessionConfigModel from './trace-session-config-model.js';

class MyAppModel {
  constructor() {
    this._traceSessions = new Map();
    this.activeSessionName = null;

    this._loadSessionProfiles();
    this.sessionDropdownModel = new KdSoftDropdownModel();

    return observable(this);
  }

  get traceSessions() { return this._traceSessions; }
  get activeSession() { return this._traceSessions.get(this.activeSessionName); }

  _loadSessionProfiles(selectName) {
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
    let selectIndex = sessionProfiles.findIndex(p => p.name === selectName);
    if (selectIndex < 0) selectIndex = 0;

    this.profileCheckListModel = new KdSoftCheckListModel(sessionProfiles, [selectIndex], false);
  }

  async openSessionFromSelectedProfile() {
    const profile = utils.first(this.profileCheckListModel.selectedEntries).item;
    if (!profile) return;

    if (this.traceSessions.has(profile.name)) return;

    const session = new TraceSession(profile);
    const success = await session.openSession();
    if (success) {
      this.traceSessions.set(profile.name, session);
      this.activeSessionName = profile.name;
    }
  }

  async closeSession(session) {
    // find they key that was inserted before (or after) the current key
    const prevKey = utils.closest(this.traceSessions.keys(), session.profile.name);

    try {
      if (session.open) {
        await session.closeRemoteSession();
      }
    } finally {
      this.traceSessions.delete(session.profile.name);
      this.activeSessionName = prevKey;
    }
  }

  getConfigModelFromSelectedProfile() {
    const profile = utils.first(this.profileCheckListModel.selectedEntries).item;
    if (!profile) return null;
    return new TraceSessionConfigModel(profile);
  }

  saveSelectedProfile(profileConfigModel) {
    const selectedProfileEntry = utils.first(this.profileCheckListModel.selectedEntries);
    if (selectedProfileEntry) {
      const profile = selectedProfileEntry.item;
      utils.setTargetProperties(profile, profileConfigModel);
      localStorage.setItem(`session-profile-${profile.name}`, JSON.stringify(profile));
    } else {
      const newProfile = profileConfigModel.cloneAsProfile();
      this.profileCheckListModel.items.push(newProfile);
      localStorage.setItem(`session-profile-${newProfile.name}`, JSON.stringify(newProfile));
    }
    this._loadSessionProfiles(selectedProfileEntry ? selectedProfileEntry.item.name : null);
  }

  deleteProfile(profileName) {
    const selectedProfileEntry = utils.first(this.profileCheckListModel.selectedEntries);
    let selectProfileName = selectedProfileEntry != null ? selectedProfileEntry.item.name : null;
    if (selectProfileName === profileName) {
      selectProfileName = null;
    }
    localStorage.removeItem(`session-profile-${profileName}`);
    this._loadSessionProfiles(selectProfileName);
  }

  saveSelectedProfileFilters(filterFormModel) {
    const profile = utils.first(this.profileCheckListModel.selectedEntries).item;
    if (profile) {
      localStorage.setItem(`session-profile-${profile.name}`, JSON.stringify(profile));
    }
  }
}

export default MyAppModel;
