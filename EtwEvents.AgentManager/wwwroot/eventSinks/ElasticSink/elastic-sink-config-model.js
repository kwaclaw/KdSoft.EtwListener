/* global i18n */

class ElasticSinkConfigModel {
  constructor() {
    this.options = {
      nodes: ['https://elastic-demo.test.com'],
      cloudId: null,
      indexFormat: 'demo-{site}-logs-{0:yyyy-MM-dd}',
    };
    this.credentials = {
      user: 'user',
      password: 'pwd',
      apiKeyId: '',
      apiKey: '',
      subjectCN: ''
    };
  }

  export() {
    //
  }
}

export default ElasticSinkConfigModel;
