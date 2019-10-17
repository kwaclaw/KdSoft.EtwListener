import { observable } from '../lib/@nx-js/observer-util.js';

class EventProvider {
  constructor(name, level, matchKeyWords = 0, disabled = false) {
    this.name = name;
    this.level = level;
    this.matchKeyWords = matchKeyWords;
    this.disabled = disabled;
  }
}

export default EventProvider;
