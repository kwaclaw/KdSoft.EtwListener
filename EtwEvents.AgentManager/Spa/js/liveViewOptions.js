class LiveViewOptions {
  constructor(standardColumns, payloadColumnList, payloadColumns) {
    this.standardColumns = standardColumns || [0, 1, 2, 3, 4, 5, 6, 7, 8];
    this.payloadColumnList = payloadColumnList || [];
    this.payloadColumns = payloadColumns || [];
  }
}

export default LiveViewOptions;
