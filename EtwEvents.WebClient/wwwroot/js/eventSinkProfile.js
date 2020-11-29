
class EventSinkProfile {
  constructor(name, type, definition) {
    this.name = name;
    this.type = type;
    this.definition = definition || {};
  }
}

export default EventSinkProfile;
