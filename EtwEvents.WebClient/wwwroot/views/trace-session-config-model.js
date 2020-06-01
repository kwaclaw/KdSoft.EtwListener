
/* global i18n */

import { html } from '../lib/lit-html.js';
import { observable, observe } from '../lib/@nx-js/observer-util.js';
import * as utils from '../js/utils.js';
import TraceSessionProfile from '../js/traceSessionProfile.js';
import FilterCarouselModel from './filter-carousel-model.js';
import KdSoftChecklistModel from './kdsoft-checklist-model.js';

const traceLevelList = () => [
  { name: i18n.__('Always'), value: 0 },
  { name: i18n.__('Critical'), value: 1 },
  { name: i18n.__('Error'), value: 2 },
  { name: i18n.__('Warning'), value: 3 },
  { name: i18n.__('Informational'), value: 4 },
  { name: i18n.__('Verbose'), value: 5 }
];

class TraceSessionConfigModel extends TraceSessionProfile {
  constructor(profile) {
    super((profile.name || '').slice(0),
      (profile.host || '').slice(0),
      utils.cloneObject([], profile.providers || []),
      utils.cloneObject([], profile.filters || []),
      profile.activeFilterIndex,
      (profile.lifeTime || '').slice(0),
      utils.cloneObject([], profile.standardColumnOrder || []),
      (profile.standardColumns || []).slice(0),
      utils.cloneObject([], profile.payloadColumnList || []),
      (profile.payloadColumns || []).slice(0)
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

    const result = observable(this);

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

    return result;
  }

  static get traceLevelList() { return observable(traceLevelList()); }

  cloneAsProfile() {
    const result = utils.cloneObject({}, this);
    Reflect.setPrototypeOf(result, TraceSessionProfile.prototype);
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
}

export default TraceSessionConfigModel;
