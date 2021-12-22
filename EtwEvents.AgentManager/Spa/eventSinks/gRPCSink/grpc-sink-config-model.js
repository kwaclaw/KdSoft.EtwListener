/* global i18n */

class gRPCSinkConfigModel {
  constructor() {
    this.options = {
      nodes: ['https://elastic-demo.test.com'],
      indexFormat: 'demo-logs-test-{0:yyyy-MM-dd}',
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

export default gRPCSinkConfigModel;
