
import { observable } from '../lib/@nx-js/observer-util/dist/es.es6.js';

function* treeNodeEntries(node, parent) {
  if (!node) return;

  const children = node.children;
  if (!children) return;

  yield { node, parent };

  for (let i = 0; i <= children.length; i += 1) {
    yield* treeNodeEntries(children[i], node);
  }
}

class KdSoftTreeNodeModel {
  constructor(id, children = [], properties = {}) {
    this.id = id;
    this.children = children;
    Object.assign(this, properties);
    return observable(this);
  }

  get treeNodeEntries() { return treeNodeEntries(this, null); }

  moveNode(fromId, toId, dropMode) {
    let fromNode; let fromParent;
    let toNode; let toParent;

    for (const entry of treeNodeEntries(this, null)) {
      if (entry.node.id === fromId) {
        fromNode = entry.node;
        fromParent = entry.parent;
      }
      if (entry.node.id === toId) {
        toNode = entry.node;
        toParent = entry.parent;
      }
    }

    if (!fromParent) return;
    if (fromNode === toNode) return;

    const fromIndx = fromParent.children.indexOf(fromNode);
    fromParent.children.splice(fromIndx, 1);

    if (!toParent) {
      if (dropMode === 'inside') {
        toParent.children.push(fromNode);
      }
      return;
    }

    const toIndx = toParent.children.indexOf(toNode);
    switch (dropMode) {
      case 'before':
        toParent.children.splice(toIndx, 0, fromNode);
        break;
      case 'after':
        toParent.children.splice(toIndx + 1, 0, fromNode);
        break;
      case 'inside':
        toNode.children.push(fromNode);
        break;
      default:
        break;
    }
  }
}

export default KdSoftTreeNodeModel;
