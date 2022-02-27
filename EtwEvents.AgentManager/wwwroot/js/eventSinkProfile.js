class EventSinkProfile {
  constructor(name, sinkType, version, batchSize, maxWriteDelayMSecs, persistentChannel, options, credentials) {
    this.name = name;
    this.sinkType = sinkType;
    this.version = version;
    this.batchSize = batchSize || 100;
    this.maxWriteDelayMSecs = maxWriteDelayMSecs || 400;
    this.persistentChannel = persistentChannel || true;
    this.options = options || {};
    this.credentials = credentials || {};
  }
}

export default EventSinkProfile;
