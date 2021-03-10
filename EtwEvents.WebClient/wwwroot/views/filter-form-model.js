import { observable } from '../lib/@nx-js/observer-util/dist/es.es6.js';
import FilterCarouselModel from './filter-carousel-model.js';

class FilterFormModel {
  constructor(session) {
    this.session = session;
    this.filterCarousel = new FilterCarouselModel(session.profile.filters, session.profile.activeFilterIndex);
    return observable(this);
  }

  postFormData() {
    const profile = this.session.profile;
    const filterModels = this.filterCarousel.filterModels;
    profile.activeFilterIndex = this.filterCarousel.activeFilterIndex;
    for (let indx = 0; indx < filterModels.length; indx += 1) {
      profile.filters[indx] = filterModels[indx].filter;
    }
    profile.filters.length = filterModels.length;
  }

  async applyActiveFilter(progress) {
    const filterModel = this.filterCarousel.activeFilterModel;
    filterModel.diagnostics = [];
    const result = await this.session.applyFilter(filterModel.filter, progress);
    if (result.success) {
      if (result.details.diagnostics.length === 0) {
        this.postFormData();
        return true;
      }
      filterModel.diagnostics = result.details.diagnostics;
    }
    return false;
  }

  async testActiveFilter(progress) {
    const filterModel = this.filterCarousel.activeFilterModel;
    filterModel.diagnostics = [];
    const result = await this.session.testFilter(filterModel.filter, progress);
    if (result.success) {
      filterModel.diagnostics = result.details.diagnostics;
    } else {
      throw (result);
    }
  }
}

export default FilterFormModel;
