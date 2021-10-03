class EventSinkProfile {
  constructor(name, sinkType, options, credentials) {
    this.name = name;
    this.sinkType = sinkType;
    this.options = options || {};
    this.credentials = credentials || {};
  }
}

export default EventSinkProfile;
