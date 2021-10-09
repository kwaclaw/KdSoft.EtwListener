/* global i18n */

class SeqSinkConfigModel {
  constructor(name, sinkType) {
    this.name = name;
    this.sinkType = sinkType;
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
