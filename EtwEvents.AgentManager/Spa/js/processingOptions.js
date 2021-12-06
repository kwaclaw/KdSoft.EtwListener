class ProcessingOptions {
  constructor(batchSize, maxWriteDelayMSecs, dynamicParts) {
    this.batchSize = batchSize || 100;
    this.maxWriteDelayMSecs = maxWriteDelayMSecs || 400;
    this.dynamicParts = dynamicParts || [];
  }
}

export default ProcessingOptions;
