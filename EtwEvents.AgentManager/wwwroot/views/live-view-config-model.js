/* global i18n */

import { observable, raw } from '@nx-js/observer-util';
import { KdsListModel } from '@kdsoft/lit-mvvm-components';
import LiveViewOptions from '../js/liveViewOptions.js';
import * as utils from '../js/utils.js';

const traceLevelList = () => [
  { name: i18n.__('Always'), value: 0 },
  { name: i18n.__('Critical'), value: 1 },
  { name: i18n.__('Error'), value: 2 },
  { name: i18n.__('Warning'), value: 3 },
  { name: i18n.__('Informational'), value: 4 },
  { name: i18n.__('Verbose'), value: 5 }
];

const standardColumnList = [
  { name: '_seqNo', label: 'Sequence No', type: 'number' },
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
  constructor(liveViewOptions) {
    this.refresh(liveViewOptions);
  }

  getSelectedStandardColumns() {
    return this.standardColumnCheckList.selectedItems;
  }

  getSelectedPayloadColumns() {
    return this.payloadColumnCheckList.selectedItems;
  }

  refresh(liveViewOptions) {
    const rawOpts = raw(liveViewOptions);
    let lvOpts = rawOpts;
    if (!(lvOpts instanceof LiveViewOptions)) {
      lvOpts = new LiveViewOptions();
      Object.assign(lvOpts, rawOpts);
    }
    lvOpts.fixup();

    const orderedStandardColumnList = standardColumnList.map((v, i, a) => a[lvOpts.standardColumnOrder[i]]);
    this.standardColumnCheckList = new KdsListModel(
      orderedStandardColumnList,
      utils.clone(lvOpts.standardColumns || [0, 1, 2, 3, 4, 5, 6, 7, 8]),
      true,
      item => item.name
    );

    this.payloadColumnCheckList = new KdsListModel(
      utils.clone(lvOpts.payloadColumnList || []),
      utils.clone(lvOpts.payloadColumns || []),
      true,
      item => item.name
    );
  }

  // when called via proxy, then this will be a proxy
  toOptions() {
    const result = new LiveViewOptions(
      raw(this.standardColumnCheckList.items.map(item => standardColumnList.findIndex(element => element.name === item.name))),
      raw(this.standardColumnCheckList.selectedIndexes),
      raw(this.payloadColumnCheckList.items),
      raw(this.payloadColumnCheckList.selectedIndexes)
    );
    return result.fixup();
  }

  static get traceLevelList() { return observable(traceLevelList()); }
  static get columnType() { return ['string', 'number', 'date', 'object']; }
}

export default LiveViewConfigModel;
