import FilterEditModel from './filter-edit-model.js';
import ProcessingOptions from '../js/processingOptions.js';

class ProcessingModel {
  constructor(processingState) {
    this.refresh(processingState);
  }

  refresh(processingState) {
    this.batchSize = processingState.batchSize;
    this.maxWriteDelayMSecs = processingState.maxWriteDelayMSecs;
    if (this.filter) {
      this.filter.refresh(processingState.filterSource);
    } else {
      this.filter = new FilterEditModel(processingState.filterSource);
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

  toProcessingOptions() {
    const result = new ProcessingOptions(this.batchSize, this.maxWriteDelayMSecs);

    let dynamicParts = this.getDynamicPartBodies();
    // if the dynamic bodies add up to an empty string, then we clear the filter
    const dynamicAggregate = dynamicParts.reduce((p, c) => ''.concat(p, c)).trim();
    if (!dynamicAggregate) dynamicParts = [];
    result.dynamicParts = dynamicParts;

    return result;
  }
}

export default ProcessingModel;
