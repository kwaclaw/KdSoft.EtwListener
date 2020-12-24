/* global i18n */

import { observable, observe } from '../../../lib/@nx-js/observer-util/dist/es.es6.js';

class ElasticSinkConfigModel {
  constructor(name, type) {
    this.name = name;
    this.type = type;
    this.definition = {
      options: observable({
        nodes: [],
        index: '',
      }),
      credentials: observable({
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

export default ElasticSinkConfigModel;
