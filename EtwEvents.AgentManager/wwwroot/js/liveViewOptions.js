class LiveViewOptions {
  constructor(standardColumnOrder, standardColumns, payloadColumnList, payloadColumns) {
    this.standardColumnOrder = standardColumnOrder;
    this.standardColumns = standardColumns;
    this.payloadColumnList = payloadColumnList;
    this.payloadColumns = payloadColumns
  }

  fixup() {
    if (!Array.isArray(this.standardColumnOrder) || this.standardColumnOrder.length !== 10) {
      this.standardColumnOrder = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
    }
    if (!Array.isArray(this.standardColumns)) {
      this.standardColumns = [0, 1, 2, 3, 4, 5, 6, 7, 8];
    }
    if (!Array.isArray(this.payloadColumnList)) {
      this.payloadColumnList = [];
    }
    if (!Array.isArray(this.payloadColumns)) {
      this.payloadColumns = [];
    }
    return this;
  }
}

export default LiveViewOptions;
