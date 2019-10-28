class TraceSessionProfile {
  constructor(name, host, providers, filters, activeFilterIndex, lifeTime) {
    this.name = name;
    this.host = host;
    this.providers = providers;
    this.filters = filters;
    this.activeFilterIndex = activeFilterIndex;
    this.lifeTime = lifeTime;
  }

  get activeFilter() { return this.filters[this.activeFilterIndex]; }
  set activeFilter(val) { this.filters[this.activeFilterIndex] = val; }
}

export default TraceSessionProfile;
