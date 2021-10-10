/* global i18n */

class ElasticSinkConfigModel {
  constructor(name, sinkType, version) {
    this.name = name;
    this.sinkType = sinkType;
    this.version = version;
    this.options = {
      nodes: [],
      index: '',
    };
    this.credentials = {
      user: '',
      password: ''
    };
  }

  export() {
    //
  }
}

export default ElasticSinkConfigModel;
