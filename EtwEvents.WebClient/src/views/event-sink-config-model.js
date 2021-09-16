﻿
/* global i18n */

import { observable, observe } from '@nx-js/observer-util/dist/es.es6.js';
import { KdSoftChecklistModel } from '@kdsoft/lit-mvvm-components';
import FetchHelper from '../js/fetchHelper.js';

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

  static async create(sinkProfile, progress) {
    const fetcher = new FetchHelper('/Etw');
    try {
      const sinkInfos = await fetcher.withProgress(progress).getJson('GetEventSinkInfos');
      const sinkTypes = sinkInfos.map(si => ({
        name: si.description,
        value: si.sinkType,
        configViewUrl: `../eventSinks/${si.configViewUrl}`,
        configModelUrl: `../eventSinks/${si.configModelUrl}`
      }));
      return new EventSinkConfigModel(sinkTypes, sinkProfile);
    } catch (error) {
      window.etwApp.defaultHandleError(error);
      return null;
    }
  }

  //get selectedSinkType() { return this.selectedSinkTypeIndex >= 0 ? this.sinkTypes[this.selectedSinkTypeIndex] : null; }
  get selectedSinkType() {
    const selIndexes = this.sinkTypeCheckListModel.selectedIndexes;
    // use result to trigger observers
    const selectedSinkTypeIndex = selIndexes.length === 0 ? -1 : selIndexes[0];
    return selectedSinkTypeIndex >= 0 ? this.sinkTypes[selectedSinkTypeIndex] : null;
  }
}

export default EventSinkConfigModel;