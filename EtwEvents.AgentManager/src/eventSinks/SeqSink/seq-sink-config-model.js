/* global i18n */

class SeqSinkConfigModel {
  constructor(name, sinkType, version) {
    this.name = name;
    this.sinkType = sinkType;
    this.version = version;
    this.options = {
      serverUrl: '',
      proxyAddress: '',
    };
    this.credentials = {
      apiKey: '',
    };
  }

  export() {
    //
  }
}

export default SeqSinkConfigModel;
