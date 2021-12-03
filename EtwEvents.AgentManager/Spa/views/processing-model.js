import FilterEditModel from './filter-edit-model.js';
import ProcessingOptions from '../js/processingOptions.js';

class ProcessingModel {
  constructor(processingState) {
    this.refresh(processingState);
  }

  refresh(processingState) {
    this.batchSize = processingState.batchSize;
    this.maxWriteDelayMSecs = processingState.maxWriteDelayMSecs;
    if (this.filter)
      this.filter.refresh(processingState.filterSource);
    else
      this.filter = new FilterEditModel(processingState.filterSource);
  }

  toProcessingOptions() {
    const result = new ProcessingOptions(this.batchSize, this.maxWriteDelayMSecs);
    result.filter.filterParts = this.filter.dynamicParts;
    return result;
  }
}

export default ProcessingModel;
