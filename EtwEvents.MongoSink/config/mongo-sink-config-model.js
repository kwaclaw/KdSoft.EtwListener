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
        eventFilterFields: ['Timestamp', 'ProviderName', 'Id', 'Level', 'Keywords', 'Opcode', 'TaskName'],
        payloadFilterFields: []
      }),
      credentials: observable({
        database: '',
        user: '',
        password: '',
        certificateCommonName: ''
      })
    };
    
    const result = observable(this);
    return result;
  }

  static get eventFields() {
    // if intended to be the source for a KdSoftChecklistModel, then the entries must be objects
    return [{ id: 'Timestamp' }, { id: 'ProviderName' }, { id: 'Channel' }, { id: 'Id' }, { id: 'Level' }, { id: 'Keywords' },
      { id: 'Opcode' }, { id: 'OpcodeName' }, { id: 'TaskName' }, { id: 'Version' }
    ];
  }

  export() {
    //
  }
}

export default MongoSinkConfigModel;
