
/* global i18n */

import { observable, observe } from '../../../lib/@nx-js/observer-util/dist/es.es6.js';

class MongoSinkConfigModel {
  constructor(name, type) {
    this.name = name;
    this.type = type;
    this.definition = {
      options: observable({
        origin: 'mongodb://',
        replicaset: '',
        database: '',
        collection: '',
        eventFilterFields: [],
        payloadFilterFields: []
      }),
      credentials: observable({
        database: '',
        user: '',
        password: ''
      })
    };
    
    const result = observable(this);
    return result;
  }

  export() {
    //
  }
}

export default MongoSinkConfigModel;
