
import { observable } from '../lib/@nx-js/observer-util.js';

class KdSoftDropDownModel {
  constructor(selectedText = '') {
    this.selectedText = selectedText;
    this.searchText = '';
    this.dropped = false;
    return observable(this);
  }
}

export default KdSoftDropDownModel;
