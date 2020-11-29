
/* global i18n */

import { observable, observe } from '../lib/@nx-js/observer-util/dist/es.es6.js';
import KdSoftChecklistModel from '../components/kdsoft-checklist-model.js';

const sinkTypeList = () => [
  { 
    name: i18n.__('File Sink'),
    value: 'FileSink',
    href: './file-sink-config.js',
    model: {
      href: './file-sink-config-model.js',
    }
   },
  {
    name: i18n.__('Mongo Sink'),
    value: 'MongoSink',
    href: './mongo-sink-config.js',
    model: {
      href: './mongo-sink-config-model.js',
    }
  },
];

class EventSinkConfigModel {
  constructor(sinkTypes, sinkProfile) {
    this.sinkTypes = sinkTypes;
    this.sinkProfile = sinkProfile;
    
    const selectedSinkTypeIndex = sinkProfile ? sinkTypes.findIndex(item => item.value == sinkProfile.type) : -1;
    this.sinkTypeCheckListModel = new KdSoftChecklistModel(
      sinkTypes,
      selectedSinkTypeIndex < 0 ? [] : [selectedSinkTypeIndex],
      false,
      item => item.value
    );

    const result = observable(this);
    return result;
  }

  //get selectedSinkType() { return this.selectedSinkTypeIndex >= 0 ? this.sinkTypes[this.selectedSinkTypeIndex] : null; }
  get selectedSinkType() { 
    const selIndexes = this.sinkTypeCheckListModel.selectedIndexes;
    // use result to trigger observers
    const selectedSinkTypeIndex = selIndexes.length === 0 ? -1 : selIndexes[0];
    return selectedSinkTypeIndex >= 0 ? this.sinkTypes[selectedSinkTypeIndex] : null;
  }

  static async create(sinkProfile) {
    //TODO replace this with async fetch from server to get a dynamic list
    const sinkTypes = sinkTypeList();
    return new EventSinkConfigModel(sinkTypes, sinkProfile);
  }
}

export default EventSinkConfigModel;
