import { KdSoftChecklist } from '@kdsoft/lit-mvvm-components';
import tailwindStyles from '../styles/tailwind-styles.js';

// Thats the only way to make the kdsoft-checklist component recognize our applications styles,
// when we need to pass styling templates to the getItemTemplate property
export default class EtwChecklist extends KdSoftChecklist {
  static get styles() {
    return [
      ...super.styles,
      KdSoftChecklist.styles,
      tailwindStyles
    ];
  }
}

window.customElements.define('etw-checklist', EtwChecklist);
