import FilterEditModel from './filter-edit-model.js';

class ProcessingModel {
  constructor(filterSource) {
    this.refresh(filterSource);
  }

  refresh(filterSource) {
    if (this.filter) {
      this.filter.refresh(filterSource);
    } else {
      this.filter = new FilterEditModel(filterSource);
    }
  }

  getDynamicPartBodies() {
    const dynamicParts = [];
    for (let indx = 0; indx < this.filter.parts.length; indx += 1) {
      const part = this.filter.parts[indx];
      if (part.name.startsWith('dynamic')) {
        // part.body only exists if the value has been set explicitly
        const partBody = Object.prototype.hasOwnProperty.call(part, 'body') ? part.body : part.lines?.map(l => l.text).join('\n');
        dynamicParts.push(partBody);
      }
    }
    return dynamicParts;
  }

  setDynamicParts(dynParts) {
    let dynIndx = 0;
    for (let indx = 0; indx < this.filter.parts.length; indx += 1) {
      const part = this.filter.parts[indx];
      if (part.name.startsWith('dynamic')) {
        part.body = dynParts[dynIndx] || '';
        part.lines = null;
        dynIndx += 1;
      }
    }
  }

  getDynamicParts() {
    let result = this.getDynamicPartBodies();
    // if the dynamic bodies add up to an empty string, then we clear the filter
    const dynamicAggregate = result.reduce((p, c) => ''.concat(p, c), '').trim();
    if (!dynamicAggregate) result = [];

    return result;
  }
}

export default ProcessingModel;
