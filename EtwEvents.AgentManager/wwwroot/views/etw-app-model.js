import { observable, observe, unobserve, raw } from '../lib/@nx-js/observer-util/dist/es.es6.js';
import RingBuffer from '../js/ringBuffer.js';
import * as utils from '../js/utils.js';
import FetchHelper from '../js/fetchHelper.js';

class EtwAppModel {
  constructor() {
    this._agentsMap = new Map();
    this._agents = [];
    this.activeAgentName = null;

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

    observe(result._updateAgentsList.bind(result));

    return result;
  }

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

  get agents() { return this._agents; }
  get activeAgent() { return this._agentsMap.get(this.activeAgentName); }

  // we need to update the array of agents in place, because we have external references (e.g. checklist)
  _updateAgentsList() {
    const ags = this._agents;
    ags.length = 0;

    // Map.values(), Map.entries() or Map.keys() don't trigger reactions when entries are assigned/set!!!
    // therefore we have to loop over the keys and get the each entry by key!
    for (const key of this._agentsMap.keys()) {
      ags.push(this._agentsMap.get(key));
    }

    ags.sort((a, b) => {
      const nameA = a.name.toUpperCase(); // ignore upper and lowercase
      const nameB = b.name.toUpperCase(); // ignore upper and lowercase
      if (nameA < nameB) {
        return -1;
      }
      if (nameA > nameB) {
        return 1;
      }
      // names must be equal
      return 0;
    });
  }

  _updateAgentsMap(agentStates) {
    const localAgentKeys = new Set(this._agentsMap.keys());

    // agentStates have unique names (case-insensitive) - //TODO server culture vs local culture?
    for (const state of (agentStates || [])) {
      const agentName = state.name.toLowerCase();
      this._agentsMap.set(agentName, state);
      localAgentKeys.delete(agentName);
    }

    // remove sessions not present on the server
    for (const agentKey of localAgentKeys) {
      this._agentsMap.delete(agentKey);
    }
  }
}

export default EtwAppModel;
