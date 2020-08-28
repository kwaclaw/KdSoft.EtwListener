
/* global i18n */

import { observable, observe } from '../lib/@nx-js/observer-util/dist/es.es6.js';
import KdSoftChecklistModel from '../components/kdsoft-checklist-model.js';

const sinkTypeList = () => [
  { name: i18n.__('File Sink'), value: 'FileSink', href: './file-sink-config.js' },
  { name: i18n.__('Mongo Sink'), value: 'MongoSink', href: './mongo-sink-config.js' },
];

class EventSinkConfigModel {
  constructor(selectedSinkTypeIndex) {
    this.selectedSinkTypeIndex = selectedSinkTypeIndex || -1;
    this.sinkTypeCheckListModel = new KdSoftChecklistModel(
      sinkTypeList(),
      // 'variable == null' checks for both, null and undefined
      this.selectedSinkTypeIndex < 0 ? [] : [this.selectedSinkTypeIndex],
      false,
      item => item.value
    );

    const result = observable(this);

    // observe checklist model changes
    this._sinkTypeListObserver = observe(() => {
      const selIndexes = result.sinkTypeCheckListModel.selectedIndexes;
      // use result to trigger observers
      result.selectedSinkTypeIndex = selIndexes.length === 0 ? -1 : selIndexes[0];
    });

    return result;
  }

  static get sinkTypeList() { return observable(sinkTypeList()); }

  export() {
    //
  }
}

export default EventSinkConfigModel;
