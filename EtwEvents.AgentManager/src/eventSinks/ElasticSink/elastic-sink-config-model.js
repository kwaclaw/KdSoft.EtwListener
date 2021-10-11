/* global i18n */

class ElasticSinkConfigModel {
  constructor() {
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
