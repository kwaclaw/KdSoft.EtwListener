/* global i18n */

import { observe } from '@nx-js/observer-util/dist/es.es6.js';
import { KdSoftChecklistModel } from '@kdsoft/lit-mvvm-components';

class EventSinkConfigModel {
  constructor(sinkInfos, agentState) {
    this.sinkInfos = sinkInfos;
    this._agentState = agentState;

    const sinkProfile = agentState.eventSink.profile;
    const selectedSinkInfoIndex = sinkProfile ? sinkInfos.findIndex(item => item.sinkType === sinkProfile.sinkType) : -1;
    this.sinkInfoCheckListModel = new KdSoftChecklistModel(
      sinkInfos,
      selectedSinkInfoIndex < 0 ? [] : [selectedSinkInfoIndex],
      false,
      item => item.sinkType
    );
  }

  get sinkProfile() {
    // need to set this up here, because we need "this" to be an observable, and it will
    // be one once this instance is accessed through a proxy (obervable) wrapper
    if (!this._profileObserver) {
      this._profileObserver = observe(() => {
        const profile = this._agentState.eventSink.profile;
        const sinkInfoIndex = profile ? this.sinkInfos.findIndex(item => item.sinkType === profile.sinkType) : -1;
        if (sinkInfoIndex < 0) this.sinkInfoCheckListModel.selectAll(false);
        else this.sinkInfoCheckListModel.selectIndex(sinkInfoIndex, true);
      });
    }
    return this._agentState.eventSink.profile;
  }
  set sinkProfile(value) { this._agentState.eventSink.profile = value; }
}

export default EventSinkConfigModel;
