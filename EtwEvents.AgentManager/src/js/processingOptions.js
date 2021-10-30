import FilterModel from './filterModel.js';

class ProcessingOptions {
  constructor(batchSize, maxWriteDelayMSecs, filter) {
    this.batchSize = batchSize || 100;
    this.maxWriteDelayMSecs = maxWriteDelayMSecs || 400;
    this.filter = filter || new FilterModel();
  }
}

export default ProcessingOptions;
