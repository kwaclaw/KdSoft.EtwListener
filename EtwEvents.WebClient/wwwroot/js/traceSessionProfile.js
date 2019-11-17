const standardColumnList = [
  { name: 'sequenceNo', label: 'Sequence No', type: 'number' },
  { name: 'taskName', label: 'Task', type: 'string' },
  { name: 'opCode', label: 'OpCode', type: 'number' },
  { name: 'timeStamp', label: 'TimeStamp', type: 'date' },
  { name: 'level', label: 'Level', type: 'number' },
  { name: 'payload', label: 'Payload', type: 'object' }
];

class TraceSessionProfile {
  constructor(name, host, providers, filters, activeFilterIndex, lifeTime,
    standardColumnOrder, standardColumns, payloadColumnList, payloadColumns
  ) {
    this.name = name;
    this.host = host;
    this.providers = providers || [];
    this.filters = filters || [];
    this.activeFilterIndex = activeFilterIndex;
    this.lifeTime = lifeTime;
    this.standardColumns = standardColumns || [];
    this.payloadColumnList = payloadColumnList;
    this.payloadColumns = payloadColumns || [];
    if (!standardColumnOrder || standardColumnOrder.length !== standardColumnList.length) {
      this.standardColumnOrder = standardColumnList.map((v, index) => index);
    } else {
      this.standardColumnOrder = standardColumnOrder;
    }
  }

  get activeFilter() { return this.filters[this.activeFilterIndex]; }
  set activeFilter(val) { this.filters[this.activeFilterIndex] = val; }

  getStandardColumnList() {
    return this.standardColumnOrder.map(order => standardColumnList[order]);
  }

  _updateStandardColumnOrder(reorderedColumns) {
    for (let indx = 0; indx < this.standardColumnOrder.length; indx += 1) {
      const reorderedCol = reorderedColumns[indx];
      const newIndex = standardColumnList.findIndex(sc => sc.name === reorderedCol.name);
      this.standardColumnOrder[indx] = newIndex;
    }
  }

  static get columnType() { return ['string', 'number', 'date', 'object']; }
}

export default TraceSessionProfile;
