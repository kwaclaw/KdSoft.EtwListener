class AgentState {
  constructor(id, site, host, enabledProviders, eventSinks, processingState) {
    this.id = id || 'id';
    this.site = site || 'Site';
    this.host = host || 'Host';
    this.enabledProviders = enabledProviders || [];
    this.eventSinks = eventSinks || {};
    this.processingState = processingState || {};
  }
}

export default AgentState;
