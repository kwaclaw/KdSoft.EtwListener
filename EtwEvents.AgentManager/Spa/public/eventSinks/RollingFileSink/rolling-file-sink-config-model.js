/* global i18n */

class RollingFileSinkConfigModel {
  constructor() {
    this.options = {
      directory: 'logs',
      fileNameFormat: 'app-{0:yyyy-MM-dd}',
      fileExtension: '.log',
      useLocalTime: true,
      fileSizeLimitKB: 4096,
      maxFileCount: 10,
      newFileOnStartup: true
    };
    this.credentials = {};
  }

  export() {
    //
  }
}

export default RollingFileSinkConfigModel;
