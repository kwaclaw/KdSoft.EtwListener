
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
    if (afIndex >= this.filters.length) return;
    this.activeFilterIndex = afIndex;
  }

  decrementActiveIndex() {
    const afIndex = this.activeFilterIndex - 1;
    if (afIndex < 0) return;
    this.activeFilterIndex = afIndex;
  }

  get activeFilterModel() { return this.filterModels[this.activeFilterIndex]; }
}

export default FilterFormModel;
