/* global i18n */

import { observable, observe } from '@nx-js/observer-util/dist/es.es6.js';
import { KdSoftChecklistModel } from '@kdsoft/lit-mvvm-components';

const traceLevelList = () => [
  { name: i18n.__('Always'), value: 0 },
  { name: i18n.__('Critical'), value: 1 },
  { name: i18n.__('Error'), value: 2 },
  { name: i18n.__('Warning'), value: 3 },
  { name: i18n.__('Informational'), value: 4 },
  { name: i18n.__('Verbose'), value: 5 }
];

const standardColumnList = [
  { name: 'sequenceNo', label: 'Sequence No', type: 'number' },
  { name: 'providerName', label: 'Provider', type: 'string' },
  { name: 'id', label: 'Id', type: 'number' },
  { name: 'keywords', label: 'Keywords', type: 'number' },
  { name: 'taskName', label: 'Task', type: 'string' },
  { name: 'opcode', label: 'Opcode', type: 'number' },
  { name: 'opcodeName', label: 'Opcode Name', type: 'string' },
  { name: 'timeStamp', label: 'TimeStamp', type: 'date' },
  { name: 'level', label: 'Level', type: 'number' },
  { name: 'payload', label: 'Payload', type: 'object' }
];

class LiveViewConfigModel {
  constructor(standardColumnOrder, standardColumns, payloadColumnList, payloadColumns) {
    this.standardColumns = standardColumns || [0, 1, 2, 3, 4, 5, 6, 7, 8];
    this.payloadColumnList = payloadColumnList;
    this.payloadColumns = payloadColumns || [];
    if (!standardColumnOrder || standardColumnOrder.length !== standardColumnList.length) {
      this.standardColumnOrder = standardColumnList.map((v, index) => index);
    } else {
      this.standardColumnOrder = standardColumnOrder;
    }

    this.standardColumnCheckList = new KdSoftChecklistModel(
      this.getStandardColumnList(),
      this.standardColumns,
      true,
      item => item.name
    );

    this.payloadColumnCheckList = new KdSoftChecklistModel(
      this.payloadColumnList,
      this.payloadColumns,
      true,
      item => item.name
    );

    // observe checklist model changes
    this._standardColumnsListObserver = observe(() => {
      // we need to update the standard colum order when the standardColumnCheckList changes it
      this._updateStandardColumnOrder(this.standardColumnCheckList.items)
      this.standardColumns = this.standardColumnCheckList.selectedIndexes;
    });

    this._payloadColumnsListObserver = observe(() => {
      this.payloadColumnList = this.payloadColumnCheckList.items;
      this.payloadColumns = this.payloadColumnCheckList.selectedIndexes;
    });
  }

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

  static get traceLevelList() { return observable(traceLevelList()); }
  static get columnType() { return ['string', 'number', 'date', 'object']; }
}

export default LiveViewConfigModel;
