import { observable, observe, unobserve } from '../lib/@nx-js/observer-util.js';
import KdSoftDropdownConnector from './kdsoft-dropdown-connector.js';

function _getSelectedText(clm) {
  let result = null;
  for (const selEntry of clm.selectedEntries) {
    if (result === null) result += `, ${selEntry.item.name}`;
    else result = selEntry.item.name;
  }
  return result;
}

class KdSoftDropdownChecklistConnector extends KdSoftDropdownConnector {
  constructor(getDropdown, getChecklist, getSelectedText) {
    super(getDropdown);
    this.getChecklist = getChecklist;
    this.getSelectedText = getSelectedText || _getSelectedText;
    return observable(this);
  }

  // override
  connectDropdownSlot() {
    super.connectDropdownSlot();

    // react to selection changes in checklist
    const selectObserver = observe(() => {
      const dropdown = this.getDropdown();
      const checklist = this.getChecklist();

      dropdown.model.selectedText = this.getSelectedText(checklist.model);
      // single select: always close up on selecting an item
      if (!checklist.model.multiSelect) {
        checklist.blur();
      }
    });

    // react to search text changes in dropdown
    const searchObserver = observe(() => {
      const dropdown = this.getDropdown();
      const checklist = this.getChecklist();

      const regex = new RegExp(dropdown.model.searchText, 'i');
      checklist.model.filter = item => regex.test(item.name);
    });

    const droppedObserver = observe(() => {
      const dropdown = this.getDropdown();
      const checklist = this.getChecklist();

      if (dropdown.model.dropped) {
        // queue this at the end of updates to be rendered correctly
        checklist.scheduler.add(() => checklist.initView());
      }
    });

    this._checkListObservers = [selectObserver, searchObserver, droppedObserver];
  }

  // override
  disconnectDropdownSlot() {
    if (this._checkListObservers) {
      this._checkListObservers.forEach(o => unobserve(o));
      this._checkListObservers = [];
    }

    super.disconnectDropdownSlot();
  }
}

export default KdSoftDropdownChecklistConnector;
