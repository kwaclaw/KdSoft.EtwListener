
// Should match the protobuf message AgentState
class AgentState {
  constructor(id, site, host, enabledProviders, eventSinks, processingState, liveViewOptions) {
    this.id = id || 'id';
    this.site = site || 'Site';
    this.host = host || 'Host';
    this.isRunning = false;
    this.isStopped = false;
    this.clientCertLifeSpan = null;
    this.enabledProviders = enabledProviders || [];
    this.processingState = processingState || {};
    this.eventSinks = eventSinks || {};
    this.liveViewOptions = liveViewOptions || {};
  }
}

export default AgentState;
