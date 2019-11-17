
import { html } from '../lib/lit-html.js';
import { observable, observe } from '../lib/@nx-js/observer-util.js';
import * as utils from './utils.js';
import TraceSessionProfile from './traceSessionProfile.js';
import FilterCarouselModel from './filter-carousel-model.js';
import KdSoftCheckListModel from './kdsoft-checklist-model.js';

const traceLevelList = [
  { name: 'Always', value: 0 },
  { name: 'Critical', value: 1 },
  { name: 'Error', value: 2 },
  { name: 'Warning', value: 3 },
  { name: 'Informational', value: 4 },
  { name: 'Verbose', value: 5 }
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

    this.standardColumnCheckList = new KdSoftCheckListModel(
      this.getStandardColumnList(),
      this.standardColumns,
      true,
      item => html`${item.label}`,
      item => item.name
    );

    this.payloadColumnCheckList = new KdSoftCheckListModel(
      this.payloadColumnList,
      this.payloadColumns,
      true,
      item => html`${item.label}`,
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

  cloneAsProfile() {
    const result = utils.cloneObject({}, this);
    Reflect.setPrototypeOf(result, TraceSessionProfile.prototype);
    return result;
  }

  static get traceLevelList() { return observable(traceLevelList.slice(0)); }
}

export default TraceSessionConfigModel;
