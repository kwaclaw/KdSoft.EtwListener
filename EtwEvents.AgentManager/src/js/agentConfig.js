class AgentConfig {
  constructor(id, site, host, enabledProviders, eventSink, filterBody) {
    this.id = id || 'id';
    this.site = site || 'Site';
    this.host = host || 'Host';
    this.enabledProviders = enabledProviders || [];
    this.eventSink = eventSink || {};
    this.filterBody = filterBody || 'return true;';
  }
}

export default AgentConfig;
