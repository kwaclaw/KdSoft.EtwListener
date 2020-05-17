import { observable } from '../lib/@nx-js/observer-util.js';

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
