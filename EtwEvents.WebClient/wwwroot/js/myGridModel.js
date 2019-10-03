
import { observable } from '../lib/@nx-js/observer-util.js';

class MyGridModel {
  constructor(columns, rows) {
    this.rows = rows;
    this.scrollIndex = 0;
    this.scrollCount = 0;
    return observable(this);
  }
}

export default MyGridModel;
