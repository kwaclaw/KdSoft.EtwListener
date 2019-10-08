
import { observable } from '../lib/@nx-js/observer-util.js';
import MyGridModel from './myGridModel.js';

class MyAppModel {
  constructor() {
    this.traceSession = null;
    this.gridModel = new MyGridModel([], []);
    this.columns = [];
    return observable(this);
  }
}

export default MyAppModel;
