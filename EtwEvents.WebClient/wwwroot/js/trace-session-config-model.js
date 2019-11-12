
import { observable } from '../lib/@nx-js/observer-util.js';
import * as utils from './utils.js';
import TraceSessionProfile from './traceSessionProfile.js';
import FilterCarouselModel from './filter-carousel-model.js';

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
      utils.cloneObject([], profile.providers),
      utils.cloneObject([], profile.filters),
      profile.activeFilterIndex,
      (profile.lifeTime || '').slice(0));

    this.filterCarousel = new FilterCarouselModel(profile.filters, profile.activeFilterIndex);
    this.activeSection = 'general';

    return observable(this);
  }

  cloneAsProfile() {
    return utils.cloneObject({}, Object.getPrototypeOf(this));
  }

  static get traceLevelList() { return observable(traceLevelList.slice(0)); }
}

export default TraceSessionConfigModel;
