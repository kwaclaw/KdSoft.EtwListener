/* global i18n */

import { observable, observe } from '../../../lib/@nx-js/observer-util/dist/es.es6.js';

class SeqSinkConfigModel {
  constructor(name, type) {
    this.name = name;
    this.type = type;
    this.definition = {
      options: observable({
        serverUrl: '',
        proxyAddress: '',
      }),
      credentials: observable({
        apiKey: '',
      })
    };
    
    const result = observable(this);
    return result;
  }

  export() {
    //
  }
}

export default SeqSinkConfigModel;
