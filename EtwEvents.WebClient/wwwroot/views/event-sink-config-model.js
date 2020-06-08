
/* global i18n */

import { observable, observe } from '../lib/@nx-js/observer-util/dist/es.es6.js';
import KdSoftChecklistModel from './kdsoft-checklist-model.js';

const sinkTypeList = () => [
  { name: i18n.__('File Sink'), value: 'FileSink' },
  { name: i18n.__('Mongo Sink'), value: 'MongoSink' },
];

class EventSinkConfigModel {
  constructor(selectedSinkTypeIndex) {
    this.selectedSinkTypeIndex = selectedSinkTypeIndex;
    this.sinkTypeCheckListModel = new KdSoftChecklistModel(
      sinkTypeList(),
      // 'variable == null' checks for both, null and undefined
      selectedSinkTypeIndex == null ? [] : [this.selectedSinkTypeIndex],
      false,
      item => item.value
    );

    const result = observable(this);

    // observe checklist model changes
    this._sinkTypeListObserver = observe(() => {
      const selIndexes = this.sinkTypeCheckListModel.selectedIndexes;
      this.selectedSinkTypeIndex = selIndexes.length === 0 ? null : selIndexes[0];
    });

    return result;
  }

  static get sinkTypeList() { return observable(sinkTypeList()); }

  export() {
    //
  }
}

export default EventSinkConfigModel;
