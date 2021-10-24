class AgentConfig {
  constructor(id, site, host, enabledProviders, eventSink, batchSize, maxWriteDelayMSecs, filterBody) {
    this.id = id || 'id';
    this.site = site || 'Site';
    this.host = host || 'Host';
    this.enabledProviders = enabledProviders || [];
    this.eventSink = eventSink || {};
    this.batchSize = batchSize || 100;
    this.maxWriteDelayMSecs = maxWriteDelayMSecs || 400;
    this.filterBody = filterBody || 'return true;';
  }
}

export default AgentConfig;
