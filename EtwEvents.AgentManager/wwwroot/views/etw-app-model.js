/* global i18n */

import { observable, observe, raw } from '@nx-js/observer-util';
import { KdSoftChecklistModel } from '@kdsoft/lit-mvvm-components';
import EventSinkProfile from '../js/eventSinkProfile.js';
import RingBuffer from '../js/ringBuffer.js';
import * as utils from '../js/utils.js';
import FetchHelper from '../js/fetchHelper.js';
import AgentState from '../js/agentState.js';
import LiveViewOptions from '../js/liveViewOptions.js';
import ProcessingModel from './processing-model.js';
import LiveViewConfigModel from './live-view-config-model.js';

const traceLevelList = () => [
  { name: i18n.__('Always'), value: 0 },
  { name: i18n.__('Critical'), value: 1 },
  { name: i18n.__('Error'), value: 2 },
  { name: i18n.__('Warning'), value: 3 },
  { name: i18n.__('Informational'), value: 4 },
  { name: i18n.__('Verbose'), value: 5 }
];

function _enhanceProviderState(provider) {
  if (!(provider.levelChecklistModel instanceof KdSoftChecklistModel)) {
    provider.levelChecklistModel = observable(new KdSoftChecklistModel(
      traceLevelList(),
      [provider.level || 0],
      false,
      item => item.value
    ));
    provider._levelObserver = observe(() => {
      provider.level = provider.levelChecklistModel.firstSelectedEntry?.item.value || 0;
    });
  }
  provider.levelChecklistModel.selectIndex(provider.level, true);
  return provider;
}

// adds view models and view related methods to agent state
function _enhanceAgentState(agentState, eventSinkInfos) {
  for (const provider of agentState.enabledProviders) {
    _enhanceProviderState(provider);
  }

  agentState.addProvider = (name, level) => {
    const newProvider = _enhanceProviderState({ name, level, matchKeywords: 0 });
    agentState.enabledProviders.splice(0, 0, newProvider);
    agentState.enabledProviders.forEach(p => {
      p.expanded = false;
    });
    newProvider.expanded = true;
  };

  agentState.removeProvider = name => {
    const index = agentState.enabledProviders.findIndex(p => p.name === name);
    if (index >= 0) agentState.enabledProviders.splice(index, 1);
  };

  if (!(agentState.processingModel instanceof ProcessingModel)) {
    agentState.processingModel = new ProcessingModel(agentState.processingState);
  } else {
    agentState.processingModel.refresh(agentState.processingState);
  }

  for (const sinkStateEntry of Object.entries(agentState.eventSinks)) {
    const sinkState = sinkStateEntry[1];
    const sinkInfo = eventSinkInfos.find(
      si => si.sinkType == sinkState.profile.sinkType && si.version == sinkState.profile.version
    );
    if (sinkInfo) {
      sinkState.configViewUrl = sinkInfo.configViewUrl;
      sinkState.configModelUrl = sinkInfo.configModelUrl;
    }
  }

  // liveViewOptions should not trigger reactions to avoid recursion
  const liveViewOptions = raw(agentState.liveViewOptions) || new LiveViewOptions();
  if (!(agentState.liveViewConfigModel instanceof LiveViewConfigModel)) {
    agentState.liveViewConfigModel = new LiveViewConfigModel(liveViewOptions);
  } else {
    agentState.liveViewConfigModel.refresh(liveViewOptions);
  }

  return agentState;
}

// we need to update the array (list) of agents in place, because we have external references (e.g. checklist)
function _updateAgentsList(agentsList, agentsMap) {
  const ags = [];

  // Map.values(), Map.entries() or Map.keys() don't trigger reactions when entries are assigned/set!!!
  // therefore we have to loop over the keys and get each entry by key!
  for (const key of agentsMap.keys()) {
    const entry = agentsMap.get(key);
    ags.push(entry);
  }

  ags.sort((a, b) => {
    const idA = a.state.id.toUpperCase(); // ignore upper and lowercase
    const idB = b.state.id.toUpperCase(); // ignore upper and lowercase
    if (idA < idB) {
      return -1;
    }
    if (idA > idB) {
      return 1;
    }
    // ids must be equal
    return 0;
  });

  agentsList.length = ags.length;
  for (let indx = 0; indx < ags.length; indx += 1) {
    agentsList[indx] = ags[indx];
  }
}

function _convertEventSinkProfiles(agentState) {
  for (const [name, sinkState] of Object.entries(agentState.eventSinks)) {
    const sinkProfile = sinkState.profile;
    sinkProfile.options = JSON.parse(sinkProfile.options);
    sinkProfile.credentials = JSON.parse(sinkProfile.credentials);
  }
}

function _updateAgentsMap(agentsMap, agentStates) {
  const localAgentKeys = new Set(agentsMap.keys());

  // agentStates have unique ids (case-insensitive) - //TODO server culture vs local culture?
  for (const state of (agentStates || [])) {
    // convert eventSinkProfile.options/credentials from a JSON string to a JSON object
    _convertEventSinkProfiles(state);

    const agentId = state.id.toLowerCase();
    let entry = agentsMap.get(agentId);
    if (!entry) {
      const newState = utils.clone(state);
      entry = { state: newState, current: state };
      Object.defineProperty(entry, 'modified', {
        get() {
          return !utils.targetEquals(entry.current, entry.state);
        }
      });
      Object.defineProperty(entry, 'disconnected', {
        get() {
          return entry.current == null;
        }
      });
      agentsMap.set(agentId, observable(entry));
    } else {
      // update only the current property of entry, the state property may be modified
      entry.current = state;
    }
    localAgentKeys.delete(agentId);
  }

  // indicate agents that are not connected anymore
  for (const agentKey of localAgentKeys) {
    const entry = agentsMap.get(agentKey);
    entry.current = null;
  }
}

function _resetProviders(agentEntry) {
  agentEntry.state.enabledProviders = utils.clone(agentEntry.current?.enabledProviders || []);
}

function _resetProcessing(agentEntry) {
  agentEntry.state.processingState = agentEntry.current?.processingState || {};
  const filterEditModel = agentEntry.state.processingModel.filter;
  filterEditModel.reset();
}

function _resetEventSinks(agentEntry) {
  const currentState = agentEntry.current;
  if (currentState) {
    agentEntry.state.eventSinks = utils.clone(currentState.eventSinks);
  } else {
    agentEntry.state.eventSinks = {};
  }
}

class EtwAppModel {
  // WE ARE CONSIDERING THIS:
  // - when a raw object is a property of an observable object, then when accessing it through the observable object,
  //   it gets itself wrapped as an observable
  // - however, when a method on the observable object manipulates the raw object internally, e.g. by adding or
  //   removing an element when the raw object is an array, then this change will not be observed by the wrapped object
  // - this means we may have to also wrap the originally raw object internally as an observable
  constructor() {
    this._agentsMap = new Map();
    this._errorSequenceNo = 0;
    this._errorTimeout = null;

    const observableThis = observable(this);
    observableThis._agents = [];
    observableThis.activeAgentId = null;

    observableThis.fetchErrors = new RingBuffer(50);
    observableThis.showLastError = false;
    observableThis.showErrors = false;

    const fetcher = new FetchHelper('/Manager');
    this.fetcher = fetcher;

    //TODO when running in dev mode with vite, we can only serve files under the root, i.e. 'Spa'
    //     so maybe we need to copy the event sink config files to a directory under Spa on AgentManager build

    observableThis.eventSinkInfos = [];
    fetcher.getJson('GetEventSinkInfos')
      .then(sinkInfos => {
        const mappedSinkInfos = sinkInfos.map(si => ({
          sinkType: si.sinkType,
          version: si.version,
          configViewUrl: `../eventSinks/${si.configViewUrl}`,
          configModelUrl: `../eventSinks/${si.configModelUrl}`
        }));
        // let's not replace eventSinkInfos, let's update its contents instead,
        // so that sinkInfoCheckListModel can observe its changes
        const esis = observableThis.eventSinkInfos;
        esis.splice(0, esis.length, ...mappedSinkInfos);
        return observableThis.getAgentStates();
      })
      .catch(error => window.etwApp.defaultHandleError(error));

    this.sinkInfoCheckListModel = new KdSoftChecklistModel(
      observableThis.eventSinkInfos,
      observableThis.eventSinkInfos.length ? [0] : [],
      false,
      item => item.sinkType
    );

    const es = new EventSource('Manager/GetAgentStates');
    es.onmessage = evt => {
      console.log(evt.data);
      const st = JSON.parse(evt.data);
      _updateAgentsMap(this._agentsMap, st.agents);
      _updateAgentsList(observableThis.agents, this._agentsMap);
    };
    es.onerror = err => {
      console.error('GetAgentStates event source error.');
    };

    return observableThis;
  }

  static get traceLevelList() { return observable(traceLevelList()); }

  handleFetchError(error) {
    const rawThis = raw(this);

    rawThis._errorSequenceNo += 1;
    if (!error.timeStamp) error.timeStamp = new Date();
    error.sequenceNo = rawThis._errorSequenceNo;

    this.fetchErrors.addItem(error);
    this.showLastError = true;
    if (rawThis._errorTimeout) window.clearTimeout(rawThis._errorTimeout);
    rawThis._errorTimeout = window.setTimeout(() => { this.showLastError = false; }, 9000);
  }

  keepErrorsOpen() {
    const rawThis = raw(this);
    if (rawThis._errorTimeout) {
      window.clearTimeout(rawThis._errorTimeout);
    }
  }

  //#region Agents

  getActiveEntry() { return raw(this)._agentsMap.get(this.activeAgentId); }

  get agents() { return this._agents; }
  get activeAgentState() {
    const activeEntry = this.getActiveEntry();
    if (!activeEntry) return null;

    return _enhanceAgentState(activeEntry.state, this.eventSinkInfos);
  }

  setAgentState(updateObject) {
    const activeEntry = this.getActiveEntry();
    if (!activeEntry) return;

    // it seems utils.setTargetProperties(activeEntry.state, updateObject) does not necessarily trigger a re-render
    utils.setTargetProperties(activeEntry.state, updateObject);
    // force re-render
    this.__changeCount++;
  }

  startEvents() {
    const agentState = this.activeAgentState;
    if (!agentState) return;
    this.fetcher.postJson('Start', { agentId: agentState.id })
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  stopEvents() {
    const agentState = this.activeAgentState;
    if (!agentState) return;
    this.fetcher.postJson('Stop', { agentId: agentState.id })
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  getEtwEvents() {
    const agentState = this.activeAgentState;
    if (!agentState) return;

    this.stopEtwEvents();
    const evs = new EventSource(`Manager/GetEtwEvents?agentId=${agentState.id}`);
    this.etwEventSource = evs;

    agentState.liveEvents = new RingBuffer(2048);
    let seqNo = 0;
    evs.onmessage = evt => {
      try {
        const etwBatch = JSON.parse(evt.data);
        for (let indx = 0; indx < etwBatch.length; indx += 1) {
          // eslint-disable-next-line no-plusplus
          etwBatch[indx]._seqNo = seqNo++;
        }
        agentState.liveEvents.addItems(etwBatch);
      } catch (err) {
        console.error(err);
      }
    };
    evs.onerror = e => {
      console.error('GetEtwEvents event source error.');
    };
  }

  stopEtwEvents() {
    const evs = this.etwEventSource;
    if (evs) {
      this.etwEventSource = null;
      evs.close();
    }
  }

  getState() {
    const agentState = this.activeAgentState;
    if (!agentState) return;
    this.fetcher.postJson('GetState', { agentId: agentState.id })
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  getAgentStates() {
    return this.fetcher.getJson('GetAgentStates')
      .then(st => {
        const rawThis = raw(this);
        _updateAgentsMap(rawThis._agentsMap, st.agents);
        _updateAgentsList(this.agents, rawThis._agentsMap);
      })
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  //#endregion

  //#region Providers

  applyProviders() {
    const agentState = this.activeAgentState;
    if (!agentState) return;

    // create "unenhanced" provider settings
    const enabledProviders = [];
    for (const enhancedProvider of agentState.enabledProviders) {
      const unenhanced = { name: undefined, level: undefined, matchKeywords: 0 };
      utils.setTargetProperties(unenhanced, enhancedProvider);
      enabledProviders.push(unenhanced);
    }

    // argument must match protobuf message ProviderSettingsList
    this.fetcher.postJson('UpdateProviders', { agentId: agentState.id }, { providerSettings: enabledProviders })
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  resetProviders() {
    const activeEntry = this.getActiveEntry();
    if (!activeEntry) return;
    _resetProviders(activeEntry);
  }

  get providersModified() {
    const activeEntry = this.getActiveEntry();
    if (!activeEntry) return false;
    return !utils.targetEquals(activeEntry.current?.enabledProviders, activeEntry.state.enabledProviders);
  }

  //#endregion

  //#region Processing

  clearFilter() {
    const agentState = this.activeAgentState;
    if (!agentState) return;
    agentState.processingModel.filter.clearDynamicParts();
  }

  testFilter() {
    const agentState = this.activeAgentState;
    if (!agentState) return;

    let dynamicParts = agentState.processingModel.getDynamicPartBodies();
    // if the dynamic bodies add up to an empty string, then we clear the filter
    const dynamicAggregate = dynamicParts.reduce((p, c) => ''.concat(p, c), '').trim();
    if (!dynamicAggregate) dynamicParts = [];

    // argument must match protobuf message TestFilterRequest
    this.fetcher.postJson('TestFilter', { agentId: agentState.id }, { dynamicParts })
      // result matches protobuf message BuildFilterResult
      .then(result => {
        const filterEditModel = agentState.processingModel.filter;
        filterEditModel.refreshSourceLines(result.filterSource);
        filterEditModel.diagnostics = result.diagnostics;
      })
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  applyProcessing() {
    const agentState = this.activeAgentState;
    if (!agentState) return;

    const processingOptions = agentState.processingModel.toProcessingOptions();

    // argument must match protobuf message TestFilterRequest
    this.fetcher.postJson('ApplyProcessingOptions', { agentId: agentState.id }, processingOptions)
      // result matches protobuf message BuildFilterResult
      .then(result => {
        const filterEditModel = agentState.processingModel.filter;
        filterEditModel.refreshSourceLines(result.filterSource);
        filterEditModel.diagnostics = result.diagnostics;
        if (result.filterSource && (!result.diagnostics || !result.diagnostics.length)) {
          agentState.processingState.filterSource = result.filterSource;
        }
      })
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  resetProcessing() {
    const activeEntry = this.getActiveEntry();
    if (!activeEntry) return;
    _resetProcessing(activeEntry);
  }

  get processingModified() {
    const activeEntry = this.getActiveEntry();
    if (!activeEntry) return false;

    const currState = activeEntry.current?.processingState || {};
    const currModel = new ProcessingModel(currState)
    const stateModel = this.activeAgentState.processingModel;
    return !utils.targetEquals(currModel, stateModel);
  }

  //#endregion

  //#region EventSink

  updateEventSinks() {
    const agentState = this.activeAgentState;
    if (!agentState) return;

    const sinkProfiles = Object.entries(agentState.eventSinks).map(es => es[1].profile);
    this.fetcher.postJson('UpdateEventSinks', { agentId: agentState.id }, sinkProfiles)
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  resetEventSinks() {
    const activeEntry = this.getActiveEntry();
    if (!activeEntry) return;
    _resetEventSinks(activeEntry);
  }

  get eventSinksModified() {
    const activeEntry = this.getActiveEntry();
    if (!activeEntry) return false;

    const currSinks = activeEntry.current?.eventSinks;
    const stateSinks = this.activeAgentState.eventSinks;
    return !utils.targetEquals(currSinks, stateSinks);
  }

  addEventSink(name, sinkInfo) {
    const agentState = this.activeAgentState;
    if (!agentState) return;

    const profile = new EventSinkProfile(name, sinkInfo.sinkType, sinkInfo.version);
    agentState.eventSinks[name] = {
      profile,
      error: null,
      configViewUrl: sinkInfo.configViewUrl,
      configModelUrl: sinkInfo.configModelUrl
    };
  }

  deleteEventSink(name) {
    const agentState = this.activeAgentState;
    if (!agentState) return;

    delete agentState.eventSinks[name];
  }

  //#endregion

  //#region Live View

  applyLiveViewConfig() {
    const agentState = this.activeAgentState;
    if (!agentState) return;

    const liveViewOptions = agentState.liveViewConfigModel.toOptions();
    this.fetcher.postJson('UpdateLiveViewOptions', { agentId: agentState.id }, liveViewOptions)
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  resetLiveViewConfig() {
    const activeEntry = this.getActiveEntry();
    if (!activeEntry) return;

    const liveViewOptions = raw(activeEntry.current?.liveViewOptions) || new LiveViewOptions();
    this.activeAgentState.liveViewConfigModel.refresh(liveViewOptions);
  }

  // sync agent state with liveViewConfigModel; we don't want to trigger reactions here
  updateLiveViewOptions(opts) {
    const activeEntry = this.getActiveEntry();
    if (!activeEntry) return false;
    raw(activeEntry.state).liveViewOptions = opts;
  }

  // this gets called typically from within render, so after liveViewConfigModel.refresh()!
  get liveViewConfigModified() {
    const activeEntry = this.getActiveEntry();
    if (!activeEntry) return false;

    const liveViewOptions = raw(activeEntry.state.liveViewConfigModel.toOptions());
    return !utils.targetEquals(activeEntry.current?.liveViewOptions, liveViewOptions);
  }

  //#endregion

  resetAll() {
    const activeEntry = this.getActiveEntry();
    if (!activeEntry) return;
    activeEntry.state = utils.clone(activeEntry.current || {});
  }
}

export default EtwAppModel;
