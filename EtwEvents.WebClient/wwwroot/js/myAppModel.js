
import { observable } from '../lib/@nx-js/observer-util.js';
import KdSoftCheckListModel from './kdsoft-checklist-model.js';
import KdSoftDropdownModel from './kdsoft-dropdown-model.js';
import TraceSessionProfile from './traceSessionProfile.js';
import EventProvider from './eventProvider.js';

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
    this.traceSession = null;

    const smartClinicProviders = [
      new EventProvider('Microsoft-Windows-Application Server-Applications', 3, 2305843009213825068),
      new EventProvider('SmartClinic-Services-Mobility', 4),
      new EventProvider('SmartClinic-Services-Interop', 4),
    ];
    const clrProviders = [new EventProvider('Microsoft-Windows-DotNETRuntime', 4)];

    const sessionProfiles = [
      new TraceSessionProfile('LocalSmartClinic', 'localhost:50051', smartClinicProviders, filterBody2, 'PT6M30S'),
      new TraceSessionProfile('LocalCLR', 'localhost:50051', clrProviders, '', 'PT6M30S')
    ];

    this.profileCheckListModel = new KdSoftCheckListModel(sessionProfiles, [1], false, item => item.name, item => item.name);
    this.sessionDropdownModel = new KdSoftDropdownModel();

    return observable(this);
  }
}

export default MyAppModel;
