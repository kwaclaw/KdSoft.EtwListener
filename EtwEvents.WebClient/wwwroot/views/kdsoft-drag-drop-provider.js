function dragStart(e) {
  e.stopPropagation();
  e.dataTransfer.setData('text/plain', this._getItemId(e.currentTarget));
  e.dataTransfer.effectAllowed = 'move';
}

function dragOver(e) {
  e.preventDefault();
}

// need to maintain a drag enter counter for the item, as the drag enter/leave event happen when a child
// node is being moved over and we know only that we left the parent element when the counter reaches 0.
function dragEnter(e) {
  e.stopPropagation();
  e.preventDefault();
  e.dataTransfer.dropEffect = 'move';

  const item = e.currentTarget;
  const dragEnterCount = item._dragEnterCount || 0;
  if (dragEnterCount <= 0) {
    item._dragEnterCount = 1;
    item.classList.add('droppable');
  } else {
    item._dragEnterCount = dragEnterCount + 1;
  }
}

function dragLeave(e) {
  e.stopPropagation();
  e.preventDefault();

  const item = e.currentTarget;
  const dragEnterCount = item._dragEnterCount || 0;
  item._dragEnterCount = dragEnterCount - 1;

  if (item._dragEnterCount <= 0) {
    item.classList.remove('droppable');
  }
}

function drop(e) {
  e.stopPropagation();
  e.preventDefault();

  const item = e.currentTarget;
  item._dragEnterCount = 0;
  item.classList.remove('droppable');

  const fromData = e.dataTransfer.getData('text/plain');
  const toData = this._getItemId(item);

  const evt = new CustomEvent('kdsoft-node-move', {
    bubbles: true,
    cancelable: true,
    composed: true,
    detail: { fromId: fromData, toId: toData }
  });
  this._element.dispatchEvent(evt);
}


class KdSoftDragDropProvider {
  constructor(getItemId) {
    this._getItemId = getItemId;

    this._dragStart = dragStart.bind(this);
    this._dragEnter = dragEnter.bind(this);
    this._dragOver = dragOver.bind(this);
    this._dragLeave = dragLeave.bind(this);
    this._drop = drop.bind(this);
  }

  connect(element) {
    this.disconnect();

    this._element = element;
    const h = this._element;

    h.addEventListener('dragstart', this._dragStart);
    h.addEventListener('dragenter', this._dragEnter);
    h.addEventListener('dragover', this._dragOver);
    h.addEventListener('dragleave', this._dragLeave);
    h.addEventListener('drop', this._drop);
  }

  disconnect() {
    if (!this.element) return;

    const h = this._element;
    this._element = null;

    h.removeEventListener('dragstart', this._dragStart);
    h.removeEventListener('dragenter', this._dragEnter);
    h.removeEventListener('dragover', this._dragOver);
    h.removeEventListener('dragleave', this._dragLeave);
    h.removeEventListener('drop', this._drop);
  }
}

export default KdSoftDragDropProvider;
