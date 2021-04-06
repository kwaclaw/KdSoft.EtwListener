/* global i18n */

import { observable, observe, unobserve, raw } from '../lib/@nx-js/observer-util/dist/es.es6.js';
import RingBuffer from '../js/ringBuffer.js';
import * as utils from '../js/utils.js';
import FetchHelper from '../js/fetchHelper.js';
import KdSoftChecklistModel from '../components/kdsoft-checklist-model.js';

const traceLevelList = () => [
  { name: i18n.__('Always'), value: 0 },
  { name: i18n.__('Critical'), value: 1 },
  { name: i18n.__('Error'), value: 2 },
  { name: i18n.__('Warning'), value: 3 },
  { name: i18n.__('Informational'), value: 4 },
  { name: i18n.__('Verbose'), value: 5 }
];

class EtwAppModel {
  constructor() {
    this._agentsMap = new Map();
    this._agents = [];
    this.activeAgentId = null;

    this._errorSequenceNo = 0;
    this.fetchErrors = new RingBuffer(50);
    this.showLastError = false;
    this.showErrors = false;

    const result = observable(this);

    this.fetcher = new FetchHelper('/Manager');
    this.fetcher.getJson('GetAgentStates')
      .then(st => result._updateAgentsMap(st.agents))
      .catch(error => window.etwApp.defaultHandleError(error));

    const es = new EventSource('Manager/GetAgentStates');
    es.onmessage = e => {
      console.log(e.data);
      const st = JSON.parse(e.data);
      result._updateAgentsMap(st.agents);
    };
    es.onerror = (e) => {
      console.error('GetAgentStates event source error.');
    };

    //observe(result._updateAgentsList.bind(result));
    observe(() => {
      result._updateAgentsList(result._agentsMap);
    });

    return result;
  }

  static get traceLevelList() { return observable(traceLevelList()); }

  handleFetchError(error) {
    this._errorSequenceNo += 1;
    if (!error.timeStamp) error.timeStamp = new Date();
    error.sequenceNo = this._errorSequenceNo;

    this.fetchErrors.addItem(error);
    this.showLastError = true;
    if (this._errorTimeout) window.clearTimeout(this._errorTimeout);
    this._errorTimeout = window.setTimeout(() => { this.showLastError = false; }, 9000);
  }

  keepErrorsOpen() {
    if (this._errorTimeout) {
      window.clearTimeout(this._errorTimeout);
    }
  }

  //#region Agents

  _enhanceProviderState(provider) {
    if (!provider.levelChecklistModel) {
      provider.levelChecklistModel = new KdSoftChecklistModel(
        traceLevelList(),
        [provider.level || 0],
        false,
        item => item.value
      );
      provider._levelObserver = observe(() => {
        provider.level = provider.levelChecklistModel.firstSelectedEntry.item.value;
      });
    }
    return provider;
  }

  // adds view models and view related methods to agent state
  _enhanceAgentState(agentState) {
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
      this._enhanceProviderState(provider);
    }

    agentState.addProvider = (name, level) => {
      const newProvider = this._enhanceProviderState({ name, level, matchKeywords: 0 });
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

    return agentState;
  }

  get agents() { return this._agents; }
  get activeAgent() {
    const entry = this._agentsMap.get(this.activeAgentId);
    if (!entry) return null;
    const result = this._enhanceAgentState(entry.state);
    return result;
  }

  // we need to update the array of agents in place, because we have external references (e.g. checklist)
  _updateAgentsList(agentsMap) {
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

    this._agents.length = ags.length;
    for (let indx = 0; indx < ags.length; indx += 1) {
      this._agents[indx] = ags[indx];
    }
  }

  _updateAgentsMap(agentStates) {
    const localAgentKeys = new Set(this._agentsMap.keys());

    // agentStates have unique ids (case-insensitive) - //TODO server culture vs local culture?
    for (const state of (agentStates || [])) {
      const agentId = state.id.toLowerCase();
      let entry = this._agentsMap.get(agentId);
      if (!entry) {
        const newState = observable(utils.clone(state));
        entry = observable({ state: newState, original: state });
        Object.defineProperty(entry, 'modified', {
          get() {
            return !utils.targetEquals(entry.original, newState);
          }
        });
        Object.defineProperty(entry, 'disconnected', {
          get() {
            return entry.original == null;
          }
        });
      } else {
        entry.original = state;
      }
      this._agentsMap.set(agentId, observable(entry));
      localAgentKeys.delete(agentId);
    }

    // indicate agents that are not connected anymore
    for (const agentKey of localAgentKeys) {
      const entry = this._agentsMap.get(agentKey);
      entry.original = null;
    }
  }

  applyProviders() {
    const agent = this.activeAgent;
    if (!agent) return;

    // create "unenhanced" provider settings
    const enabledProviders = [];
    for (const enhancedProvider of agent.enabledProviders) {
      const unenhanced = { name: undefined, level: undefined, matchKeywords: 0 };
      utils.setTargetProperties(unenhanced, enhancedProvider);
      enabledProviders.push(unenhanced);
    }

    // argument must match protobuf message ProviderSettingsList
    this.fetcher.postJson('UpdateProviders', { agentId: agent.id }, { providerSettings: enabledProviders })
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  resetProviders() {
    const activeEntry = this._agentsMap.get(this.activeAgentId);
    if (!activeEntry) return;

    const freshProviders = [];
    for (const provider of activeEntry.original.enabledProviders) {
      freshProviders.push(this._enhanceProviderState(provider));
    }
    activeEntry.state.enabledProviders = freshProviders;
  }

  testFilter() {
    const agent = this.activeAgent;
    if (!agent) return;

    // argument must match protobuf message TestFilterRequest
    this.fetcher.postJson('TestFilter', { agentId: agent.id }, { csharpFilter: agent.filterModel.filter })
      // result matches protobuf message BuildFilterResult
      .then(result => {
        agent.filterModel.diagnostics = result.diagnostics;
      })
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  applyFilter() {
    const agent = this.activeAgent;
    if (!agent) return;

    // argument must match protobuf message TestFilterRequest
    this.fetcher.postJson('ApplyFilter', { agentId: agent.id }, { csharpFilter: agent.filterModel.filter })
      // result matches protobuf message BuildFilterResult
      .then(result => {
        agent.filterModel.diagnostics = result.diagnostics;
      })
      .catch(error => window.etwApp.defaultHandleError(error));
  }

  resetFilter() {
    const activeEntry = this._agentsMap.get(this.activeAgentId);
    if (!activeEntry) return;

    const filterModel = activeEntry.state.filterModel;
    filterModel.filter = activeEntry.original.filterBody;
    filterModel.diagnostics = [];
  }
}

export default EtwAppModel;
