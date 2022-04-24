
// Should match the protobuf message AgentConfig
class AgentRawOptions {
  constructor(enabledProviders, dynamicFilterParts, eventSinkProfiles, liveViewOptions) {
    this.enabledProviders = enabledProviders || null;
    this.dynamicFilterParts = dynamicFilterParts || null;
    this.eventSinkProfiles = eventSinkProfiles || null;
    this.liveViewOptions = liveViewOptions || null;
  }
}

export default AgentRawOptions;
