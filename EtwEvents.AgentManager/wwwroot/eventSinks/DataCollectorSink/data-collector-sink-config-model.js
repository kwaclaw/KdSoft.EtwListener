/* global i18n */

class DataCollectorSinkConfigModel {
  constructor() {
    this.options = {
      customerId: '',
      logType: '',
      resourceId: '',
    };
    this.credentials = {
      sharedKey: '',
    };
  }

  export() {
    //
  }
}

export default DataCollectorSinkConfigModel;
