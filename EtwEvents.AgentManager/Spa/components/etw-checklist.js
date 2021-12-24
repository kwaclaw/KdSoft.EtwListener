import { KdSoftChecklist } from '@kdsoft/lit-mvvm-components';
import tailwindStyles from '../styles/tailwind-styles.js';

// Thats the only way to make the kdsoft-checklist component recognize our applications styles,
// when need for styling templates passed to the getItemTemplate property
export default class EtwChecklist extends KdSoftChecklist {
  static get styles() {
    return [
      KdSoftChecklist.styles,
      tailwindStyles
    ];
  }
}

window.customElements.define('etw-checklist', EtwChecklist);
