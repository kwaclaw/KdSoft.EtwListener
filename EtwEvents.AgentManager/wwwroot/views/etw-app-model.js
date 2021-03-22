import { observable, observe, unobserve, raw } from '../lib/@nx-js/observer-util/dist/es.es6.js';
import RingBuffer from '../js/ringBuffer.js';
import * as utils from '../js/utils.js';
import FetchHelper from '../js/fetchHelper.js';

class EtwAppModel {
  constructor() {
    this._agents = observable(new Map());
    this.activeAgentName = null;

    this._errorSequenceNo = 0;
    this.fetchErrors = new RingBuffer(50);
    this.showLastError = false;
    this.showErrors = false;

    this.fetcher = new FetchHelper('/Manager');
    this.fetcher.getJson('GetAgentStates')
      .then(st => this._updateAgents(st.agents))
      .catch(error => window.etwApp.defaultHandleError(error));

    const es = new EventSource('Manager/GetAgentStates');
    es.onmessage = e => {
      console.log(e.data);
      const st = JSON.parse(e.data);
      this._updateAgents(st.agents);
    };
    es.onerror = (e) => {
      console.error('GetAgentStates event source error.');
    };

    return observable(this);
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
  get activeAgent() { return this._agents.get(this.activeAgentName); }

  _updateAgents(agentStates) {
    const localAgentKeys = new Set(this.agents.keys());

    // sessionStates have unique names (case-insensitive) - //TODO server culture vs local culture?
    for (const state of (agentStates || [])) {
      const agentName = state.name.toLowerCase();
      const agent = this.agents.get(agentName);

      if (agent) {
        agent.updateState(state);
      } else {
        this.agents.set(agentName, state);
      }

      localAgentKeys.delete(agentName);
    }

    // remove sessions not present on the server
    for (const agentKey of localAgentKeys) {
      this.agents.delete(agentKey);
    }
  }
}

export default EtwAppModel;
