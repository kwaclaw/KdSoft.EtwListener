/* global i18n */

import { KdSoftChecklistModel } from '@kdsoft/lit-mvvm-components';

class EventSinkConfigModel {
  constructor(sinkInfos, sinkProfile, sinkError) {
    this.sinkInfos = sinkInfos;
    this.sinkProfile = sinkProfile;
    this.sinkError = sinkError;

    const selectedSinkInfoIndex = sinkProfile ? sinkInfos.findIndex(item => item.sinkType == sinkProfile.sinkType) : -1;
    this.sinkInfoCheckListModel = new KdSoftChecklistModel(
      sinkInfos,
      selectedSinkInfoIndex < 0 ? [] : [selectedSinkInfoIndex],
      false,
      item => item.sinkType
    );
  }

  //get selectedSinkType() { return this.selectedSinkTypeIndex >= 0 ? this.sinkTypes[this.selectedSinkTypeIndex] : null; }
  get selectedSinkInfo() {
    const selIndexes = this.sinkInfoCheckListModel.selectedIndexes;
    // use result to trigger observers
    const selectedSinkInfoIndex = selIndexes.length === 0 ? -1 : selIndexes[0];
    return selectedSinkInfoIndex >= 0 ? this.sinkInfos[selectedSinkInfoIndex] : null;
  }
}

export default EventSinkConfigModel;
