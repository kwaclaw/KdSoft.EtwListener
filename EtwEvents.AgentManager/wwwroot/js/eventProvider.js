class EventProvider {
  constructor(name, level, matchKeywords) {
    this.name = name || 'Event Provider';
    // "variable == null" tests for both, null and undefined,
    // because (null == undefined) is true!, but (null === undefined) is false
    this.level = level == null ? 0 : level;
    this.matchKeywords = matchKeywords == null ? 0 : matchKeywords;
  }
}

export default EventProvider;
