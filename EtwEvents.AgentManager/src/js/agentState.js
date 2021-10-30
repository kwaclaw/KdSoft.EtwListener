import ProcessingOptions from './processingOptions.js';

class AgentState {
  constructor(id, site, host, enabledProviders, eventSink, processingOptions) {
    this.id = id || 'id';
    this.site = site || 'Site';
    this.host = host || 'Host';
    this.enabledProviders = enabledProviders || [];
    this.eventSink = eventSink || {};
    this.processingOptions = processingOptions || new ProcessingOptions();
  }
}

export default AgentState;
