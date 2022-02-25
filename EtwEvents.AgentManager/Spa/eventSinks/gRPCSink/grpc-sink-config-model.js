/* global i18n */

class gRPCSinkConfigModel {
  constructor() {
    this.options = {
      host: '',
      maxSendMessageSize: null,
      maxReceiveMessageSize: null,
      maxRetryAttempts: null,
      maxRetryBufferSize: null,
      maxRetryBufferPerCallSize: null
    };
    this.credentials = {
      certificatePem: '',
      certificateKeyPem: '',
      certificateThumbPrint: '',
      certificateSubjectCN: ''
    };
  }

  export() {
    //
  }
}

export default gRPCSinkConfigModel;
