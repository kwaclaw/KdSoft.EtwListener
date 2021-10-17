/* global i18n */

import { observable, observe, raw } from '@nx-js/observer-util/dist/es.es6.js';
import { KdSoftChecklistModel } from '@kdsoft/lit-mvvm-components';
import EventSinkConfigModel from './event-sink-config-model.js';
import RingBuffer from '../js/ringBuffer.js';
import * as utils from '../js/utils.js';
import FetchHelper from '../js/fetchHelper.js';

const traceLevelList = () => [
  { name: i18n.__('Always'), value: 0 },
  { name: i18n.__('Critical'), value: 1 },
  { name: i18n.__('Error'), value: 2 },
  { name: i18n.__('Warning'), value: 3 },
  { name: i18n.__('Informational'), value: 4 },
  { name: i18n.__('Verbose'), value: 5 }
];

function _enhanceProviderState(provider) {
  if (!provider.levelChecklistModel) {
    provider.levelChecklistModel = observable(new KdSoftChecklistModel(
      traceLevelList(),
      [provider.level || 0],
      false,
      item => item.value
    ));
    provider._levelObserver = observe(() => {
      provider.level = provider.levelChecklistModel.firstSelectedEntry.item.value;
    });
  }
  return provider;
}

// adds view models and view related methods to agent state, agentState must be observable
function _enhanceAgentState(agentState, eventSinkInfos) {
  if (!agentState.filterModel) {
    agentState.filterModel = {
      filter: agentState.filterBody,
      diagnostics: []
    };
    agentState._filterObserver = observe(() => {
      agentState.filterBody = agentState.filterModel.filter;
    });
  }

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

  if (!agentState.sinkConfigModel) {
    agentState.sinkConfigModel = new EventSinkConfigModel(eventSinkInfos, agentState);
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

function _updateAgentsMap(agentsMap, agentStates) {
  const localAgentKeys = new Set(agentsMap.keys());

  // agentStates have unique ids (case-insensitive) - //TODO server culture vs local culture?
  for (const state of (agentStates || [])) {
    const agentId = state.id.toLowerCase();
    let entry = agentsMap.get(agentId);
    if (!entry) {
      const newState = utils.clone(state);
      entry = { state: newState, current: state };
      Object.defineProperty(entry, 'modified', {
        get() {
          return !utils.targetEquals(entry.current, newState);
        }
      });
      Object.defineProperty(entry, 'disconnected', {
        get() {
          return entry.current == null;
        }
      });
    } else {
      // update but do not replace existing state, as it may have been enhanced already
      entry.current = state;
    }
    agentsMap.set(agentId, observable(entry));
    localAgentKeys.delete(agentId);
  }

  // indicate agents that are not connected anymore
  for (const agentKey of localAgentKeys) {
    const entry = this._agentsMap.get(agentKey);
    entry.current = null;
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

    console.log(`etw-app-model: fetcher route: ${fetcher.route}.`);

    //TODO when running in dev mode with vite, we can only serve files under the root, i.e. 'src'
    //     so maybe we need to copy the event sink config files to a directory under src on AgentManager build

    observableThis.eventSinkInfos = [];
    fetcher.getJson('GetEventSinkInfos')
      .then(sinkInfos => {
        const mappedSinkInfos = sinkInfos.map(si => ({
          sinkType: si.sinkType,
          version: si.version,
          configViewUrl: `../eventSinks/${si.configViewUrl}`,
          configModelUrl: `../eventSinks/${si.configModelUrl}`
        }));
        observableThis.eventSinkInfos = mappedSinkInfos;
        return observableThis.getAgentStates();
      })
      .catch(error => window.etwApp.defaultHandleError(error));

    const es = new EventSource('Manager/GetAgentStates');
    es.onmessage = e => {
      console.log(e.data);
      const st = JSON.parse(e.data);
      _updateAgentsMap(this._agentsMap, st.agents);
      _updateAgentsList(observableThis.agents, this._agentsMap);
    };
    es.onerror = (e) => {
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

  get agents() { return this._agents; }
  get activeAgentState() {
    const entry = raw(this)._agentsMap.get(this.activeAgentId);
    if (!entry) return null;

    entry.state = _enhanceAgentState(entry.state, this.eventSinkInfos);
    return entry.state;
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

  getAgentStates() {
    return this.fetcher.getJson('GetAgentStates')
      .then(st => {
        const rawThis = raw(this);
        _updateAgentsMap(rawThis._agentsMap, st.agents);
        _updateAgentsList(this.agents, rawThis._agentsMap);
      })
      .catch(error => window.etwApp.defaultHandleError(error));
  }

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
    const activeEntry = raw(this)._agentsMap.get(this.activeAgentId);
    if (!activeEntry) return;

    const freshProviders = [];
    for (const provider of activeEntry.current.enabledProviders) {
      freshProviders.push(_enhanceProviderState(provider));
    }
    activeEntry.state.enabledProviders = freshProviders;
  }

  testFilter() {
    const agentState = this.activeAgentState;
    if (!agentState) return;

    // argument must match protobuf message TestFilterRequest
    this.fetcher.postJson('TestFilter', { agentId: agentState.id }, { csharpFilter: agentState.filterModel.filter })
      // result matches protobuf message BuildFilterResult
      .then(result => {
        agentState.filterModel.diagnostics = result.diagnostics;
      })
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  applyFilter() {
    const agentState = this.activeAgentState;
    if (!agentState) return;

    // argument must match protobuf message TestFilterRequest
    this.fetcher.postJson('ApplyFilter', { agentId: agentState.id }, { csharpFilter: agentState.filterModel.filter })
      // result matches protobuf message BuildFilterResult
      .then(result => {
        agentState.filterModel.diagnostics = result.diagnostics;
      })
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  resetFilter() {
    const activeEntry = raw(this)._agentsMap.get(this.activeAgentId);
    if (!activeEntry) return;

    const filterModel = activeEntry.state.filterModel;
    filterModel.filter = activeEntry.current.filterBody;
    filterModel.diagnostics = [];
  }

  updateEventSink() {
    const agentState = this.activeAgentState;
    if (!agentState) return;

    const sinkConfigModel = agentState.sinkConfigModel;
    this.fetcher.postJson('UpdateEventSink', { agentId: agentState.id }, sinkConfigModel.sinkProfile)
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  resetEventSink() {
    const activeEntry = raw(this)._agentsMap.get(this.activeAgentId);
    if (!activeEntry) return;

    const currentState = activeEntry.current;
    activeEntry.state.eventSink = utils.clone(currentState.eventSink);
  }

  get eventSinkModified() {
    const activeEntry = raw(this)._agentsMap.get(this.activeAgentId);
    if (!activeEntry) return false;
    return !utils.targetEquals(activeEntry.current.eventSink, activeEntry.state.eventSink);
  }
}

export default EtwAppModel;
