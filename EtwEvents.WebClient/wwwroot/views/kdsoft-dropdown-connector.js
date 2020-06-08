import { observable } from '../lib/@nx-js/observer-util/dist/es.es6.js';

class KdSoftDropdownConnector {
  constructor(getDropdown) {
    this.getDropdown = getDropdown;
    return observable(this);
  }

  // override this
  connectDropdownSlot() {
    //
  }

  // override this
  disconnectDropdownSlot() {
    //
  }
}

export default KdSoftDropdownConnector;
