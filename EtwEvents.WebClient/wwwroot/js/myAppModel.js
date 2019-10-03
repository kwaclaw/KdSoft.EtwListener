
import { observable } from '../lib/@nx-js/observer-util.js';
import MyGridModel from './myGridModel.js';

class MyAppModel {
  constructor() {
    this.loadTime = 0;
    this.parseTime = 0;
    this.renderTime = 0;
    this.gridModel = new MyGridModel([], []);
    this.columns = [];
    return observable(this);
  }
}

export default MyAppModel;
