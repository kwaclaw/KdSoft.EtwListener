/* global i18n */

class LogsIngestionSinkConfigModel {
  constructor() {
    this.options = {
      endPoint: '',
      ruleId: '',
      streamName: '',
    };
    this.credentials = {
      tenantId: '',
      clientId: '',
      clientSecret: {
        secret: '',
      },
      clientCertificate: {
        certificatePem: '',
        certificateKeyPem: '',
        certificateThumbprint: '',
        certificateSubjectCN: '',
      },
      usernamePassword: {
        username: '',
        password: '',
      }
    };
  }

  export() {
    //
  }
}

export default LogsIngestionSinkConfigModel;
