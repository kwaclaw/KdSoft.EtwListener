
import { observable, observe } from '../lib/@nx-js/observer-util.js';

class FilterFormModel {
  constructor(session) {
    this.filters = session.profile.filters;
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

  //TODO how is active filter index set?

  async applyFilter() {
    await this.session.applyFilter(this.activeFilterIndex);
  }
}

export default FilterFormModel;
