/* global i18n */

import { observable, observe, unobserve } from '../lib/@nx-js/observer-util/dist/es.es6.js';
import * as utils from '../js/utils.js';
import TraceSessionProfile from '../js/traceSessionProfile.js';
import FilterCarouselModel from './filter-carousel-model.js';
import KdSoftChecklistModel from '../components/kdsoft-checklist-model.js';

const traceLevelList = () => [
  { name: i18n.__('Always'), value: 0 },
  { name: i18n.__('Critical'), value: 1 },
  { name: i18n.__('Error'), value: 2 },
  { name: i18n.__('Warning'), value: 3 },
  { name: i18n.__('Informational'), value: 4 },
  { name: i18n.__('Verbose'), value: 5 }
];

function enhanceProviderState(provider) {
  if (!(provider.levelChecklistModel instanceof KdSoftChecklistModel)) {
    provider.levelChecklistModel = new KdSoftChecklistModel(
      traceLevelList(),
      [provider.level || 0],
      false,
      item => item.value
    );
  }
  if (provider._levelObserver) {
    unobserve(provider._levelObserver);
  }
  provider._levelObserver = observe(() => {
    provider.level = provider.levelChecklistModel.firstSelectedEntry.item.value;
  });

  return provider;
}


class TraceSessionConfigModel extends TraceSessionProfile {
  constructor(profile, eventSinkProfiles) {
    super((profile.name || '').slice(0),
      (profile.host || '').slice(0),
      utils.clone(profile.providers || []),
      utils.clone(profile.filters || []),
      profile.activeFilterIndex,
      (profile.lifeTime || '').slice(0),
      (profile.batchSize || 100),
      (profile.maxWriteDelayMS || 300),
      utils.clone(profile.standardColumnOrder || []),
      (profile.standardColumns || []).slice(0),
      utils.clone(profile.payloadColumnList || []),
      (profile.payloadColumns || []).slice(0),
      (profile.eventSinks || []).slice(0)
    );

    this.filterCarousel = new FilterCarouselModel(profile.filters, profile.activeFilterIndex);
    this.activeSection = 'general';

    this.standardColumnCheckList = new KdSoftChecklistModel(
      this.getStandardColumnList(),
      this.standardColumns,
      true,
      item => item.name
    );

    this.payloadColumnCheckList = new KdSoftChecklistModel(
      this.payloadColumnList,
      this.payloadColumns,
      true,
      item => item.name
    );

    const eventSinkIndex = es => {
      const name = es.name.toLowerCase();
      const type = es.type.toLowerCase();
      return eventSinkProfiles.findIndex(esp => name === esp.name.toLowerCase() && type === es.type.toLowerCase());
    };

    this.eventSinkCheckList = new KdSoftChecklistModel(
      eventSinkProfiles,
      this.eventSinks.map(es => eventSinkIndex(es)).filter(esi => esi >= 0),
      true,
      item => `${item.type}:${item.name}`
    );

    const result = observable(this);

    result.providers.forEach(provider => enhanceProviderState(provider));

    // observe checklist model changes
    this._standardColumnsListObserver = observe(() => {
      // we need to update the standard colum order when the standardColumnCheckList changes it
      this._updateStandardColumnOrder(this.standardColumnCheckList.items)
      this.standardColumns = this.standardColumnCheckList.selectedIndexes;
    });

    this._payloadColumnsListObserver = observe(() => {
      this.payloadColumnList = this.payloadColumnCheckList.items;
      this.payloadColumns = this.payloadColumnCheckList.selectedIndexes;
    });

    this._eventSinkListObserver = observe(() => {
      this.eventSinks = this.eventSinkCheckList.selectedItems;
    });

    return result;
  }

  static get traceLevelList() { return observable(traceLevelList()); }

  cloneAsProfile() {
    // an empty instance establishes the prototype
    const result = new TraceSessionProfile(); // establishes the prototype
    utils.setTargetProperties(result, this);
    return result;
  }

  exportProfile() {
    const profileToExport = new TraceSessionProfile();
    utils.setTargetProperties(profileToExport, this);
    const profileString = JSON.stringify(profileToExport, null, 2);
    const profileURL = `data:text/plain,${profileString}`;

    const a = document.createElement('a');
    try {
      a.style.display = 'none';
      a.href = profileURL;
      a.download = `${profileToExport.name}.json`;
      document.body.appendChild(a);
      a.click();
    } finally {
      document.body.removeChild(a);
    }
  }

  addProvider(name, level) {
    const newProvider = enhanceProviderState({ name, level, matchKeywords: 0, disabled: false });
    this.providers.splice(0, 0, newProvider);
    this.providers.forEach(p => {
      p.expanded = false;
    });
    newProvider.expanded = true;
  }

  removeProvider(name) {
    const index = this.providers.findIndex(p => p.name === name);
    if (index >= 0) this.providers.splice(index, 1);
  }
}

export default TraceSessionConfigModel;
