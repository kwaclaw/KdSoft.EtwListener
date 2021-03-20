
class Spinner {
  constructor(element) {
    this._element = element;
  }

  start() {
    this._element.classList.add('spinning');
  }

  stop() {
    this._element.classList.remove('spinning');
  }
}

export default Spinner;
