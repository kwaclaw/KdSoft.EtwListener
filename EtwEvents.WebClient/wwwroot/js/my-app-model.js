
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

    const sessionProfiles = [];
    for (let i = 0; i < localStorage.length; i += 1) {
      const key = localStorage.key(i);
      if (key.startsWith('session-profile-')) {
        const item = JSON.parse(localStorage.getItem(key));
        const profile = new TraceSessionProfile();
        utils.assignTargetProperties(profile, item);
        sessionProfiles.push(profile);
      }
    }

    this.profileCheckListModel = new KdSoftCheckListModel(sessionProfiles, [1], false, item => item.name, item => item.name);
    this.sessionDropdownModel = new KdSoftDropdownModel();

    return observable(this);
  }

  get traceSessions() { return this._traceSessions; }
  get activeSession() { return this._traceSessions.get(this.activeSessionName); }

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
    const profile = utils.first(this.profileCheckListModel.selectedEntries).item;
    if (profile) {
      utils.assignTargetProperties(profile, profileConfigModel);
      localStorage.setItem(`session-profile-${profile.name}`, JSON.stringify(profile));
    } else {
      const newProfile = profileConfigModel.cloneAsProfile();
      this.profileCheckListModel.items.push(newProfile);
      localStorage.setItem(`session-profile-${newProfile.name}`, JSON.stringify(newProfile));
    }
  }

  saveSelectedProfileFilters(filterFormModel) {
    const profile = utils.first(this.profileCheckListModel.selectedEntries).item;
    if (profile) {
      profile.filters = filterFormModel.filters;
      profile.activeFilterIndex = filterFormModel.activeFilterIndex;
      localStorage.setItem(`session-profile-${profile.name}`, JSON.stringify(profile));
    }
  }
}

export default MyAppModel;
