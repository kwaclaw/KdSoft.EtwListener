
import { observable, observe } from '../lib/@nx-js/observer-util.js';

class FilterFormModel {
  constructor(session) {
    this.session = session;
    this.filters = session.profile.filters.slice(0);
    this.activeFilterIndex = session.profile.activeFilterIndex;
    this.editFilterModels = [];

    const formModel = observable(this);
    for (let indx = 0; indx < this.filters.length; indx += 1) {
      const editModel = {
        index: indx,
        filter: this.filters[indx],
      };
      const observableEditModel = observable(editModel);
      this.editFilterModels.push(observableEditModel);
      // update form model when filter gets edited
      editModel.observer = observe(() => {
        this.filters[indx] = observableEditModel.filter;
        console.log(`Filter changed: ${observableEditModel.filter}`);
      });
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

  applyActiveFilter() {
    return this.session.applyFilter(this.filters[this.activeFilterIndex]);
  }

  testActiveFilter() {
    return this.session.testFilter(this.filters[this.activeFilterIndex]);
  }
}

export default FilterFormModel;
