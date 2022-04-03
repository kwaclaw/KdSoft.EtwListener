/* global i18n */

import { observable, observe, raw } from '@nx-js/observer-util';
import { KdSoftChecklistModel } from '@kdsoft/lit-mvvm-components';
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
    this._liveViewOptions = raw(liveViewOptions) || new LiveViewOptions();
    this.refresh(liveViewOptions);
  }

  getSelectedStandardColumns() {
    return this.standardColumnCheckList.selectedItems;
  }

  getSelectedPayloadColumns() {
    return this.payloadColumnCheckList.selectedItems;
  }

  refresh(liveViewOptions) {
    const lvOpts = raw(liveViewOptions || new LiveViewOptions());
    this.standardColumnCheckList = new KdSoftChecklistModel(
      utils.clone(standardColumnList),
      utils.clone(lvOpts.standardColumns || [0, 1, 2, 3, 4, 5, 6, 7, 8]),
      true,
      item => item.name
    );

    this.payloadColumnCheckList = new KdSoftChecklistModel(
      utils.clone(lvOpts.payloadColumnList || []),
      utils.clone(lvOpts.payloadColumns || []),
      true,
      item => item.name
    );
  }

  toOptions() {
    return this._liveViewOptions;
  }

  static get traceLevelList() { return observable(traceLevelList()); }
  static get columnType() { return ['string', 'number', 'date', 'object']; }
}

export default LiveViewConfigModel;
