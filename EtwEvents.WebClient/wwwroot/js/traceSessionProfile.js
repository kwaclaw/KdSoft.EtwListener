﻿const standardColumnList = [
  { name: 'sequenceNo', label: 'Sequence No', type: 'number' },
  { name: 'taskName', label: 'Task', type: 'string' },
  { name: 'opCode', label: 'OpCode', type: 'number' },
  { name: 'timeStamp', label: 'TimeStamp', type: 'date' },
  { name: 'level', label: 'Level', type: 'number' },
  { name: 'payload', label: 'Payload', type: 'object' }
];

class TraceSessionProfile {
  constructor(name, host, providers, filters, activeFilterIndex, lifeTime, standardColumns, payloadColumnList, payloadColumns) {
    this.name = name;
    this.host = host;
    this.providers = providers || [];
    this.filters = filters || [];
    this.activeFilterIndex = activeFilterIndex;
    this.lifeTime = lifeTime;
    this.getStandardColumnList = () => standardColumnList.slice(0);
    this.standardColumns = standardColumns || [];
    this.payloadColumnList = payloadColumnList;
    this.payloadColumns = payloadColumns || [];
  }

  get activeFilter() { return this.filters[this.activeFilterIndex]; }
  set activeFilter(val) { this.filters[this.activeFilterIndex] = val; }

}

export default TraceSessionProfile;
