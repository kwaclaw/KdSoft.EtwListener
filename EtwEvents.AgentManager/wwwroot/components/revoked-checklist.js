import { css } from '@kdsoft/lit-mvvm';
import EtwChecklist from './etw-checklist.js';

// Thats the only way to make the kdsoft-checklist component recognize our applications styles,
// when we need to pass styling templates to the getItemTemplate property
export default class RevokedChecklist extends EtwChecklist {
  static get styles() {
    return [
      ...super.styles,
      css`
        .revoked-entry .thumb-print {
          width:14em;
          text-overflow:ellipsis;
          overflow:hidden;
          white-space:nowrap;
        }
        .revoked-entry button {
          color: #718096;
        }
      `
    ];
  }
}

window.customElements.define('revoked-checklist', RevokedChecklist);

//TODO rework lit-mvvm-components:
// - to inject a style
// - or to have a slot for styles
// - or to expose parts to be stylable
