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
    const selectedIndexes = sessionProfiles.length > 0 ? [selectIndex] : [];

    this.profileCheckListModel = new KdSoftCheckListModel(sessionProfiles, selectedIndexes, false);
  }

  async openSessionFromSelectedProfile() {
    const profileEntry = utils.first(this.profileCheckListModel.selectedEntries);
    if (!profileEntry) return;

    const profile = profileEntry.item;

    if (this.traceSessions.has(profile.name)) return;

    const session = new TraceSession(profile);
    const success = await session.openSession();
    if (!success) return;

    this.traceSessions.set(profile.name, session);
    this.activeSessionName = profile.name;

    const filter = profile.activeFilter;
    if (filter) {
      const result = await session.applyFilter(filter);
      if (result.success) {
        if (result.details.diagnostics.length > 0) {
          //TODO show the error somehow
          console.log(JSON.stringify(result.details.diagnostics));
        }
      }
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
    const profileEntry = utils.first(this.profileCheckListModel.selectedEntries);
    const profile = profileEntry ? profileEntry.item : new TraceSessionProfile('New Profile');
    return new TraceSessionConfigModel(profile);
  }

  saveProfile(profileModel) {
    const profileToSave = new TraceSessionProfile();
    // save profile with only the properties that are defined in the class
    utils.setTargetProperties(profileToSave, profileModel);
    localStorage.setItem(`session-profile-${profileToSave.name}`, JSON.stringify(profileToSave));
  }

  saveSelectedProfile(profileConfigModel) {
    const selectedProfileEntry = utils.first(this.profileCheckListModel.selectedEntries);
    let profileModel = null;

    if (selectedProfileEntry) {
      // copy changes to our selected profile
      profileModel = selectedProfileEntry.item;
      if (profileConfigModel) {
        utils.setTargetProperties(profileModel, profileConfigModel);
      }
    } else if (profileConfigModel) {
      profileModel = profileConfigModel.cloneAsProfile();
      this.profileCheckListModel.items.push(profileModel);
    }

    if (profileModel) {
      this.saveProfile(profileModel);
      this._loadSessionProfiles(selectedProfileEntry ? selectedProfileEntry.item.name : null);
    }
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

  async importProfiles(files) {
    const selectedProfileEntry = utils.first(this.profileCheckListModel.selectedEntries);

    for (let i = 0; i < files.length; i += 1) {
      const file = files[i];
      const json = await file.text();
      try {
        const profile = JSON.parse(json);
        this.saveProfile(profile);
      } catch (err) {
        console.log(err);
      }
    }
    
    this._loadSessionProfiles(selectedProfileEntry ? selectedProfileEntry.item.name : null);
  }
}

export default MyAppModel;
