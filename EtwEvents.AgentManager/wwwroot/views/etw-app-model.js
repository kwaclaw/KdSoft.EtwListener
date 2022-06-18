/* global i18n */

import { observable, observe, raw } from '@nx-js/observer-util';
import { KdSoftChecklistModel } from '@kdsoft/lit-mvvm-components';
import EventSinkProfile from '../js/eventSinkProfile.js';
import RingBuffer from '../js/ringBuffer.js';
import * as utils from '../js/utils.js';
import FetchHelper from '../js/fetchHelper.js';
import AgentRawOptions from '../js/agentRawOptions.js';
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
  try {
    for (const provider of agentState.enabledProviders) {
      _enhanceProviderState(provider);
    }

    if (!agentState.addProvider) {
      agentState.addProvider = (name, level) => {
        const newProvider = _enhanceProviderState({ name, level, matchKeywords: 0 });
        agentState.enabledProviders.splice(0, 0, newProvider);
        agentState.enabledProviders.forEach(p => {
          p.expanded = false;
        });
        newProvider.expanded = true;
      };
    }

    if (!agentState.removeProvider) {
      agentState.removeProvider = name => {
        const index = agentState.enabledProviders.findIndex(p => p.name === name);
        if (index >= 0) agentState.enabledProviders.splice(index, 1);
      };
    }

    if (!(agentState.processingModel instanceof ProcessingModel)) {
      agentState.processingState ||= {
        filterSource: {
          templateVersion: 0,
          sourceLines: [],
          dynamicLineSpans: []
        }
      };
      agentState.processingModel = new ProcessingModel(agentState.processingState.filterSource);
    } else {
      agentState.processingModel.refresh(agentState.processingState.filterSource);
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
  catch (error) {
    console.error(error);
    // prevent infinite recursion via re-rendering etw-app.js
    window.queueMicrotask(() => window.etwApp.model.handleAppError(error));
    return null;
  }
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

  ags.sort((a, b) => utils.compareIgnoreCase(a.state.id, b.state.id));

  agentsList.length = ags.length;
  for (let indx = 0; indx < ags.length; indx += 1) {
    agentsList[indx] = ags[indx];
  }
}

function _convertEventSinkProfiles(agentState) {
  agentState.eventSinks ||= {};
  for (const [name, sinkState] of Object.entries(agentState.eventSinks)) {
    const sinkProfile = sinkState.profile;
    sinkProfile.options = JSON.parse(sinkProfile.options);
    sinkProfile.credentials = JSON.parse(sinkProfile.credentials);
  }
}

function _copyEventSinkStatuses(agentEntry) {
  for (const [name, sinkState] of Object.entries(agentEntry.state.eventSinks)) {
    const currentState = agentEntry.current.eventSinks[name];
    if (currentState) {
      sinkState.status = currentState.status
    }
  }
}

function _updateAgentsMap(agentsMap, agentStates) {
  const localAgentKeys = new Set(agentsMap.keys());

  // agentStates have unique ids (case-insensitive) - //TODO server culture vs local culture?
  for (const state of (agentStates || [])) {
    // providers should be sorted to allow proper "modified" detection
    state.enabledProviders ||= [];
    state.enabledProviders.sort((a, b) => utils.compareIgnoreCase(a.name, b.name));

    // convert eventSinkProfile.options/credentials from a JSON string to a JSON object
    _convertEventSinkProfiles(state);

    const agentId = state.id.toLowerCase();
    let entry = agentsMap.get(agentId);
    if (!entry) {
      const newState = utils.clone(state);
      entry = { state: newState, current: state };
      Object.defineProperty(entry, 'modified', {
        get() {
          // we ignore the properties isRunning, isStopped, and clientCertLifeSpan for comparison
          const rawState = raw(entry.state);
          const oldIsRunning = rawState.isRunning;
          const oldIsStopped = rawState.isStopped;
          const oldClientCertLifeSpan = rawState.clientCertLifeSpan;
          rawState.isRunning = entry.current.isRunning;
          rawState.isStopped = entry.current.isStopped;
          rawState.clientCertLifeSpan = entry.current.clientCertLifeSpan;
          const result = !utils.targetEquals(raw(entry.current), rawState);
          rawState.isRunning = oldIsRunning;
          rawState.isStopped = oldIsStopped;
          rawState.clientCertLifeSpan = oldClientCertLifeSpan;
          return result;
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
      // we need to copy the current eventsink status to entry.state, as it is not a configuration item but a real-time status
      _copyEventSinkStatuses(entry);
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

function _resetProcessingOptions(agentEntry) {
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

  handleAppError(error) {
    if (error && error instanceof Error) {
      error = { title: `JS: ${error.message || 'application error'}` };
    } else if (!error || typeof error !== 'object') {
      error = { title: `JS: ${error || 'application error'}` };
    }

    this.handleFetchError(error);
  }

  keepErrorsOpen() {
    const rawThis = raw(this);
    if (rawThis._errorTimeout) {
      window.clearTimeout(rawThis._errorTimeout);
    }
  }

  //#region Agents

  get agents() { return this._agents; }

  getActiveEntry() { return raw(this)._agentsMap.get(this.activeAgentId); }

  get activeAgentState() {
    const activeEntry = this.getActiveEntry();
    if (!activeEntry) return null;

    return _enhanceAgentState(activeEntry.state, this.eventSinkInfos);
  }

  setAgentState(entry, updateObject) {
    if (!entry) return;

    // it seems utils.setTargetProperties(activeEntry.state, updateObject) does not necessarily trigger a re-render
    utils.setTargetProperties(entry.state, updateObject);

    // special case - need to update the filter foreground
    const filterEditModel = entry.state.processingModel.filter;
    if (updateObject.processingState) {
      filterEditModel.refreshSourceLines(updateObject.processingState.filterSource);
    }
    filterEditModel.diagnostics = [];

    // force re-render
    this.__changeCount++;
  }

  startEvents(currentState) {
    if (!currentState) return;
    this.fetcher.postJson('Start', { agentId: currentState.id })
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  stopEvents(currentState) {
    if (!currentState) return;
    this.fetcher.postJson('Stop', { agentId: currentState.id })
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  resetAgent(currentState) {
    if (!currentState) return;
    this.fetcher.postJson('Reset', { agentId: currentState.id })
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  getEtwEvents(agentState) {
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

  refreshState(agentState) {
    if (!agentState) {
      return;
    }
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

  getEnabledProviders(agentState) {
    // create "unenhanced" provider settings
    const enabledProviders = [];
    for (const enhancedProvider of agentState.enabledProviders) {
      const unenhanced = { name: undefined, level: undefined, matchKeywords: 0 };
      utils.setTargetProperties(unenhanced, enhancedProvider);
      enabledProviders.push(unenhanced);
    }
    return enabledProviders;
  }

  applyProviders(agentState) {
    if (!agentState) {
      return;
    }
    // providers should be sorted to allow proper "modified" detection
    agentState.enabledProviders.sort((a, b) => utils.compareIgnoreCase(a.name, b.name));

    const opts = new AgentRawOptions();
    opts.enabledProviders = this.getEnabledProviders(agentState);
    this.fetcher.postJson('ApplyAgentOptions', { agentId: agentState.id }, opts)
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  resetProviders(entry) {
    if (!entry) {
      return;
    }
    _resetProviders(entry);
  }

  getProvidersModified(entry) {
    if (!entry) {
      return false;
    }
    return !utils.targetEquals(entry.current?.enabledProviders, entry.state.enabledProviders);
  }

  //#endregion

  //#region Processing

  clearFilter(agentState) {
    if (!agentState) {
      return;
    }
    agentState.processingModel.filter.clearDynamicParts();
  }

  testFilter(agentState) {
    if (!agentState) {
      return;
    }
    let dynamicParts = agentState.processingModel.getDynamicParts();
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

  applyProcessingOptions(agentState) {
    if (!agentState) {
      return;
    }
    const opts = new AgentRawOptions();
    opts.dynamicFilterParts = agentState.processingModel.getDynamicParts();
    this.fetcher.postJson('ApplyAgentOptions', { agentId: agentState.id }, opts)
      .then(result => {
        // filterResult matches protobuf message BuildFilterResult
        const filterResult = result.filterResult;
        const filterEditModel = agentState.processingModel.filter;
        filterEditModel.refreshSourceLines(filterResult.filterSource);
        filterEditModel.diagnostics = filterResult.diagnostics;
        if (filterResult.filterSource && (!filterResult.diagnostics || !filterResult.diagnostics.length)) {
          agentState.processingState.filterSource = filterResult.filterSource;
        }
      })
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  resetProcessingOptions(entry) {
    if (!entry) {
      return;
    }
    _resetProcessingOptions(entry);
  }

  getProcessingModified(entry) {
    if (!entry) {
      return false;
    }
    const currState = entry.current?.processingState;
    const currParts = (new ProcessingModel(currState?.filterSource)).getDynamicPartBodies();
    const stateParts = entry.state.processingModel.getDynamicPartBodies();
    return !utils.targetEquals(currParts, stateParts);
  }

  //#endregion

  //#region EventSink

  updateEventSinks(agentState) {
    if (!agentState) {
      return;
    }
    const opts = new AgentRawOptions();
    opts.eventSinkProfiles = Object.entries(agentState.eventSinks).map(es => es[1].profile);
    this.fetcher.postJson('ApplyAgentOptions', { agentId: agentState.id }, opts)
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  resetEventSinks(entry) {
    if (!entry) {
      return;
    }
    _resetEventSinks(entry);
  }

  getEventSinksModified(entry) {
    if (!entry) {
      return false;
    }
    const currProfiles = Object.entries(entry.current?.eventSinks).map(es => es[1].profile);
    const stateProfiles = Object.entries(entry.state.eventSinks).map(es => es[1].profile);
    return !utils.targetEquals(currProfiles, stateProfiles);
  }

  addEventSink(agentState, name, sinkInfo) {
    if (!agentState) {
      return;
    }
    const profile = new EventSinkProfile(name, sinkInfo.sinkType, sinkInfo.version);
    agentState.eventSinks[name] = {
      profile,
      error: null,
      configViewUrl: sinkInfo.configViewUrl,
      configModelUrl: sinkInfo.configModelUrl
    };
  }

  deleteEventSink(agentState, name) {
    if (!agentState) return;
    delete agentState.eventSinks[name];
  }

  //#endregion

  //#region Live View

  applyLiveViewOptions(agentState) {
    if (!agentState) {
      return;
    }
    const opts = new AgentRawOptions();
    opts.liveViewOptions = agentState.liveViewConfigModel.toOptions();
    this.fetcher.postJson('ApplyAgentOptions', { agentId: agentState.id }, opts)
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  resetLiveViewOptions(entry) {
    if (!entry) {
      return;
    }
    const liveViewOptions = raw(entry.current?.liveViewOptions) || new LiveViewOptions();
    entry.state.liveViewConfigModel.refresh(liveViewOptions);
  }

  // sync agent state with liveViewConfigModel; we don't want to trigger reactions here
  updateLiveViewOptions(entry, opts) {
    if (!entry) {
      return false;
    }
    raw(entry.state).liveViewOptions = opts;
  }

  // this gets called typically from within render, so after liveViewConfigModel.refresh()!
  getLiveViewOptionsModified(entry) {
    if (!entry) {
      return false;
    }
    const liveViewOptions = raw(entry.state.liveViewConfigModel.toOptions());
    return !utils.targetEquals(entry.current?.liveViewOptions, liveViewOptions);
  }

  //#endregion

  getAgentOptions(agentState) {
    const result = new AgentRawOptions();
    result.enabledProviders = this.getEnabledProviders(agentState);
    result.dynamicFilterParts = agentState.processingModel.getDynamicParts();
    result.eventSinkProfiles = Object.entries(agentState.eventSinks).map(es => es[1].profile);
    result.liveViewOptions = agentState.liveViewConfigModel.toOptions();
    return result;
  }

  setAgentOptions(agentState, options) {
    agentState.enabledProviders = options.enabledProviders;
    agentState.processingModel.setDynamicParts(options.dynamicFilterParts);
    const eventSinkStates = [];
    for (const profile of options.eventSinkProfiles) {
      const eventSinkState = { profile, status: undefined };
      const sinkInfo = this.eventSinkInfos.find(
        si => si.sinkType == profile.sinkType && si.version == profile.version
      );
      if (sinkInfo) {
        eventSinkState.configViewUrl = sinkInfo.configViewUrl;
        eventSinkState.configModelUrl = sinkInfo.configModelUrl;
      }
      eventSinkStates.push(eventSinkState);
    }
    agentState.eventSinks = eventSinkStates;
    agentState.liveViewOptions = options.liveViewOptions;
  }

  applyAllOptions(agentState) {
    if (!agentState) {
      return;
    }
    const options = this.getAgentOptions(agentState);
    this.fetcher.postJson('ApplyAgentOptions', { agentId: agentState.id }, options)
      .then(result => {
        // filterResult matches protobuf message BuildFilterResult
        const filterResult = result.filterResult;
        if (filterResult) {
          const filterEditModel = agentState.processingModel.filter;
          filterEditModel.refreshSourceLines(filterResult.filterSource);
          filterEditModel.diagnostics = filterResult.diagnostics;
          if (filterResult.filterSource && (!filterResult.diagnostics || !filterResult.diagnostics.length)) {
            agentState.processingState.filterSource = filterResult.filterSource;
          }
        }
      })
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  resetAllOptions(entry) {
    if (!entry || !entry.current) {
      return;
    }
    this.setAgentState(entry, utils.clone(entry.current));
  }
}

export default EtwAppModel;
