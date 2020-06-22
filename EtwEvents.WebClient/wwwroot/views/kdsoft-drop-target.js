import KdSoftDragDropProvider from './kdsoft-drag-drop-provider.js';

class KdSoftDropTarget extends HTMLDivElement {
  constructor() {
    super();
    this.dragdrop = new KdSoftDragDropProvider(item => item.id);
    this.dragdrop.connect(this);
  }

  attributeChangedCallback(name, oldValue, newValue) {
    if (name === 'class') {
      // console.log(`oldValue: ${oldValue}, newValue: ${newValue}`);
    }
  }

  static get observedAttributes() { return ['class']; }
}

window.customElements.define('kdsoft-drop-target', KdSoftDropTarget, { extends: 'div' });
