/* global i18n */

class ElasticSinkConfigModel {
  constructor() {
    this.options = {
      nodes: ['https://elastic-demo.test.com'],
      indexFormat: 'demo-logs-test-{0:yyyy-MM-dd}',
    };
    this.credentials = {
      user: 'user',
      password: 'pwd',
      apiKey: '',
      subjectCN: ''
    };
  }

  export() {
    //
  }
}

export default ElasticSinkConfigModel;
