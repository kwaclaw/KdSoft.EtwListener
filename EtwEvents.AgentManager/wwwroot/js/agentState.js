class AgentState {
  constructor(id, site, host, enabledProviders, eventSinks, processingState, liveViewOptions) {
    this.id = id || 'id';
    this.site = site || 'Site';
    this.host = host || 'Host';
    this.enabledProviders = enabledProviders || [];
    this.eventSinks = eventSinks || {};
    this.processingState = processingState || {};
    this.liveViewOptions = liveViewOptions || {}
  }
}

export default AgentState;
