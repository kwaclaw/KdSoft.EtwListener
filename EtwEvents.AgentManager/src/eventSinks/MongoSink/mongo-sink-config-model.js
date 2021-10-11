/* global i18n */

class MongoSinkConfigModel {
  constructor() {
    this.options = {
      origin: 'mongodb://',
      replicaset: '',
      database: '',
      collection: '',
      eventFilterFields: ['Timestamp', 'ProviderName', 'Id', 'Level', 'Keywords', 'Opcode', 'TaskName'],
      payloadFilterFields: []
    };
    this.credentials = {
      database: '',
      user: '',
      password: '',
      certificateCommonName: ''
    };
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
