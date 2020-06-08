
import { observable } from '../lib/@nx-js/observer-util/dist/es.es6.js';

class KdSoftDropdownModel {
  constructor(selectedText = '') {
    this.selectedText = selectedText;
    this.searchText = '';
    this.dropped = false;
    return observable(this);
  }
}

export default KdSoftDropdownModel;
