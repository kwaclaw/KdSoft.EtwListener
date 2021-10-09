/* global i18n */

class ElasticSinkConfigModel {
  constructor(name, sinkType) {
    this.name = name;
    this.sinkType = sinkType;
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
