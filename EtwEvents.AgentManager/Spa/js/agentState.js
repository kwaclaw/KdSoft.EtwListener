class AgentState {
  constructor(id, site, host, enabledProviders, eventSink, processingState) {
    this.id = id || 'id';
    this.site = site || 'Site';
    this.host = host || 'Host';
    this.enabledProviders = enabledProviders || [];
    this.eventSink = eventSink || {};
    this.processingState = processingState || {};
  }
}

export default AgentState;
