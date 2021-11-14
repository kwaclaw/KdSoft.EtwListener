class EventSinkProfile {
  constructor(name, sinkType, version, options, credentials) {
    this.name = name;
    this.sinkType = sinkType;
    this.version = version;
    this.options = options || {};
    this.credentials = credentials || {};
  }
}

export default EventSinkProfile;
