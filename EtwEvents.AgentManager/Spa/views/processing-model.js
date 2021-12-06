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

  getDynamicParts() {
    const dynamicParts = [];
    for (let indx = 0; indx < this.filter.dynamicParts.length; indx += 1) {
      const part = this.filter.dynamicParts[indx];
      dynamicParts.push(part.lines?.join('\n'));
    }
    return dynamicParts;
  }

  toProcessingOptions() {
    const result = new ProcessingOptions(this.batchSize, this.maxWriteDelayMSecs);
    result.dynamicParts = this.getDynamicParts();
    return result;
  }
}

export default ProcessingModel;
