import { observable } from '../lib/@nx-js/observer-util/dist/es.es6.js';

class FilterCarouselModel {
  constructor(filters, activeFilterIndex) {
    this.activeFilterIndex = activeFilterIndex;
    this.filterModels = [];

    const carouselModel = observable(this);
    for (let indx = 0; indx < filters.length; indx += 1) {
      const editModel = {
        index: indx,
        filter: filters[indx].slice(0),
        diagnostics: [],
      };
      const observableEditModel = observable(editModel);
      this.filterModels.push(observableEditModel);
    }

    return carouselModel;
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

export default FilterCarouselModel;
