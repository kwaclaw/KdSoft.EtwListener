
import { observable } from '../lib/@nx-js/observer-util.js';
import KdSoftCheckListModel from './kdsoft-checklist-model.js';
import KdSoftDropdownModel from './kdsoft-dropdown-model.js';
import TraceSession from './traceSession.js';
import TraceSessionProfile from './traceSessionProfile.js';
import EventProvider from './eventProvider.js';
import * as utils from './utils.js';

const filterBodyBase = `
var appDomain = evt.PayloadStringByName("AppDomain");
var result = appDomain == "MobilityServicesHost.exe" || appDomain == "MobilityPlatformClient.exe";
`;

const filterBody = `${filterBodyBase}
return result || (evt.ProviderName != "Microsoft-Windows-Application Server-Applications")
`;

const filterBody2 = `${filterBodyBase}
var hostref = evt.PayloadStringByName("HostReference") ?? string.Empty;
result = result
  || hostref.StartsWith("MobilityServices")
  || (evt.ProviderName != "Microsoft-Windows-Application Server-Applications");

var duration = evt.PayloadByName("Duration");
result = result || (duration != null && (long)duration > 0);

// result = result && evt.Level <= TraceEventLevel.Informational;

return result;
`;


class MyAppModel {
  constructor() {
    this._traceSessions = new Map();
    this.activeSessionName = null;

    // const smartClinicProviders = [
    //   new EventProvider('Microsoft-Windows-Application Server-Applications', 0, 2305843009213825068),
    //   new EventProvider('SmartClinic-Services-Mobility', 4),
    //   new EventProvider('SmartClinic-Services-Interop', 4),
    // ];
    // const clrProviders = [new EventProvider('Microsoft-Windows-DotNETRuntime', 4)];

    // const sessionProfiles = [
    //   new TraceSessionProfile('LocalSmartClinic', 'localhost:50051', smartClinicProviders, [filterBody2], 0, 'PT6M30S'),
    //   new TraceSessionProfile('LocalCLR', 'localhost:50051', clrProviders, [], null, 'PT6M30S')
    // ];

    // for (const sp of sessionProfiles) {
    //   localStorage.setItem(`session-profile-${sp.name}`, JSON.stringify(sp));
    // }

    const sessionProfiles = [];
    for (let i = 0; i < localStorage.length; i += 1) {
      const key = localStorage.key(i);
      if (key.startsWith('session-profile-')) {
        const item = JSON.parse(localStorage.getItem(key));
        sessionProfiles.push(item);
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

  cloneObservableSelectedProfile() {
    const profile = utils.first(this.profileCheckListModel.selectedEntries).item;
    if (!profile) return null;
    return observable(utils.cloneObject({}, profile));
  }

  saveSelectedProfile(newProfile) {
    const profile = utils.first(this.profileCheckListModel.selectedEntries).item;
    if (profile) {
      utils.assignExistingProperties(profile, newProfile);
      localStorage.setItem(`session-profile-${profile.name}`, JSON.stringify(profile));
    } else {
      this.profileCheckListModel.items.push(newProfile);
      localStorage.setItem(`session-profile-${newProfile.name}`, JSON.stringify(newProfile));
    }
  }
}

export default MyAppModel;
