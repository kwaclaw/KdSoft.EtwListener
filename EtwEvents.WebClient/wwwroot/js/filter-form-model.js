
import { observable, observe } from '../lib/@nx-js/observer-util.js';

class FilterFormModel {
  constructor(session) {
    this.session = session;
    this.activeFilterIndex = session.profile.activeFilterIndex;
    this.filterModels = [];

    const formModel = observable(this);
    for (let indx = 0; indx < session.profile.filters.length; indx += 1) {
      const editModel = {
        index: indx,
        filter: session.profile.filters[indx].slice(0),
        diagnostics: [],
      };
      const observableEditModel = observable(editModel);
      this.filterModels.push(observableEditModel);
    }

    return formModel;
  }

  incrementActiveIndex() {
    const afIndex = this.activeFilterIndex + 1;
    if (afIndex >= this.filterModels.length) return;
    this.activeFilterIndex = afIndex;
  }

  decrementActiveIndex() {
    const afIndex = this.activeFilterIndex - 1;
    if (afIndex < 0) return;
    this.activeFilterIndex = afIndex;
  }

  postFormData() {
    const profile = this.session.profile;
    profile.activeFilterIndex = this.activeFilterIndex;
    for (let indx = 0; indx < this.filterModels.length; indx += 1) {
      profile.filters[indx] = this.filterModels[indx].filter;
    }
  }

  removeActiveFilter() {
    this.filterModels.splice(this.activeFilterIndex, 1);
    if (this.activeFilterIndex >= this.filterModels.length) {
      this.activeFilterIndex = this.filterModels.length - 1;
    }
  }

  addFilterModel() {
    const newModel = {
      index: this.filterModels.length,
      filter: '',
      diagnostics: [],
    };
    this.filterModels.push(newModel);
    this.activeFilterIndex = this.filterModels.length - 1;
  }

  get activeFilterModel() { return this.filterModels[this.activeFilterIndex]; }
}

export default FilterFormModel;
