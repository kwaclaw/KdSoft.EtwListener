/* global i18n */

class ElasticSinkConfigModel {
  constructor() {
    this.options = {
      nodes: ['https://elastic-demo.test.com'],
      index: 'demo-logs-test-{0:yyyy-MM-dd}',
    };
    this.credentials = {
      user: 'user',
      password: 'pwd'
    };
  }

  export() {
    //
  }
}

export default ElasticSinkConfigModel;
