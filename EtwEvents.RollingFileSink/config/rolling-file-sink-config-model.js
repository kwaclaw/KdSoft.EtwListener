/* global i18n */

import { observable, observe } from '../../../lib/@nx-js/observer-util/dist/es.es6.js';

class RollingFileSinkConfigModel {
  constructor(name, type) {
    this.name = name;
    this.type = type;
    this.definition = {
      options: observable({
        directory: 'logs',
        fileNameFormat: 'app-{0:yyyy-MM-dd}',
        fileExtension: '.log',
        useLocalTime: true,
        fileSizeLimitKB: 4096,
        maxFileCount: 10,
        newFileOnStartup: true
      }),
      credentials: observable({})
    };
    
    const result = observable(this);
    return result;
  }

  export() {
    //
  }
}

export default RollingFileSinkConfigModel;
