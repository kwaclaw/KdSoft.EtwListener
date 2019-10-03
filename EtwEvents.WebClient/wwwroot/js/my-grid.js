import { html } from '../lib/lit-html.js';
import { repeat } from '../lib/lit-html/directives/repeat.js';
import { LitMvvmElement, BatchScheduler } from '../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../lib/@nx-js/queue-util.js';
import { css, unsafeCSS } from '../styles/css-tag.js';
import { SyncFusionGridStyle } from '../styles/css-grid-syncfusion-style.js';

// These are the elements needed by this element.

const _headerTemplate = new WeakMap();
const _rowTemplate = new WeakMap();
const _gridTemplateColumns = new WeakMap();

class MyGrid extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
  }

  get headerTemplate() {
    return _headerTemplate.get(this) || html``;
  }

  set headerTemplate(value) {
    _headerTemplate.set(this, value);
  }

  get rowTemplate() {
    return _rowTemplate.get(this) || html``;
  }

  set rowTemplate(value) {
    _rowTemplate.set(this, value);
  }

  get gridTemplateColumns() {
    return _gridTemplateColumns.get(this) || '';
  }

  set gridTemplateColumns(value) {
    _gridTemplateColumns.set(this, value);
  }

  static get styles() {
    return [
      SyncFusionGridStyle,
      css`
        #gridContainer {
          position: relative;
          height: 100%;
        }
        #grid {
          position: relative;
          grid-template-columns: var(--column-defs);
          height: 100%;
          overflow-x: auto;
          overflow-y: auto;
          -webkit-overflow-scrolling: touch;
          pointer-events: auto;
          z-index: 20;
        }
        .stretcher {
          position: absolute;
          left: 0;
          align-self: stretch;
          height: var(--scroll-height);
        }
      `,
    ];
  }

  _findBoundaries(sb) {
    let firstHiddenIndex = null;
    let firstVisibleIndex = null;
    let isBelow = false;
    const topRows = [];

    // const scrollTop = sb.scrollTop + headerheight
    const scrollBottom = sb.scrollTop + sb.offsetHeight;

    for (let i = 0; i < sb.children.length; i += 1) {
      const child = sb.children[i];
      if (child.className.includes('sfg-row')) {
        topRows.push(child);
        if (child.firstElementChild.offsetTop < sb.scrollTop) {
          // (partially) hidden
          if (firstHiddenIndex == null) {
            firstHiddenIndex = topRows.length - 1;
          }
        } else if (child.firstElementChild.offsetTop < scrollBottom) {
          if (firstVisibleIndex == null) {
            // fully or partially  visible
            firstVisibleIndex = topRows.length - 1;
            if (firstHiddenIndex != null) break;
          }
        } else if (firstHiddenIndex == null && firstVisibleIndex == null) {
          // all rows below bottom border
          firstHiddenIndex = topRows.length - 1;
          isBelow = true;
          break;
        } else if (firstHiddenIndex == null) {
          firstHiddenIndex = topRows.length - 1;
          break;
        }
      }
    }

    return {
      topRows,
      firstHiddenIndex,
      firstVisibleIndex,
      isBelow, // all rows are below bottom border
    };
  }

  _calculateScrollPos(sb, gridClientHeight) {
    const windowRowCount = Math.floor(gridClientHeight / 34);
    let newScrollIndex = 0;
    const scrollCount = windowRowCount * 2;

    const bounds = this._findBoundaries(sb);
    if (bounds.topRows.length) {
      const overhangCount = Math.floor(windowRowCount / 2);

      if (bounds.firstVisibleIndex != null) {

        let stayInPlaceIndex = bounds.firstVisibleIndex;
        const stayInPlaceRow = bounds.topRows[stayInPlaceIndex];
        const stayInPlaceOffset = stayInPlaceRow.firstElementChild.offsetTop;

        const firstRowOffset = bounds.topRows[0].firstElementChild.offsetTop;
        const offsetFromScrollTop = firstRowOffset - sb.scrollTop;
        let deltaRows = Math.floor(offsetFromScrollTop / 34) + overhangCount;
        newScrollIndex = this.model.scrollIndex - deltaRows;
        if (newScrollIndex < 0) {
          deltaRows += newScrollIndex; // apply correction
          newScrollIndex = 0;
        }
        stayInPlaceIndex += deltaRows;

        this._adjustTopCallback = () => this._adjustTopForSmallDistance(sb, stayInPlaceIndex, stayInPlaceOffset);

      } else { // big jump, no connecting elements

        newScrollIndex = Math.floor(sb.scrollTop / 34) - overhangCount;
        if (newScrollIndex < 0) newScrollIndex = 0;

        this._adjustTopCallback = () => this._adjustTopForBigDistance(sb, overhangCount);

      }

      if (newScrollIndex >= this.model.rows.length) newScrollIndex = this.model.rows.length - 1;
    }

    console.log(`Scrolled: scrollTop: ${sb.scrollTop}, visible:${bounds.firstVisibleIndex} - indx:${newScrollIndex}`);

    this._windowRowCount = windowRowCount;

    this.model.scrollIndex = newScrollIndex;
    this.model.scrollCount = scrollCount;
  }

  _processTopAdjustment() {
    const callback = this._adjustTopCallback;
    if (callback) {
      this._adjustTopCallback = null;
      callback();
    }
  }

  rendered() {
    console.log(`Rendered`);
    this._processTopAdjustment();
  }

  _adjustTopForBigDistance(sb, overhangCount) {
    if (sb.querySelector) {
      this._adjusting = true;
      const topOffset = sb.scrollTop - (34 * overhangCount);
      this.renderRoot.getElementById('top-edge').style.height = `${topOffset}px`;
      console.log(`Adjusted-big: topOffset:${topOffset} - indx:${this.model.scrollIndex}`);
      return;
    }
    console.log(`Not Adjusted-big: indx:${this.model.scrollIndex}`);
  }

  _getRowByIndex(sb, rowIndex) {
    let index = -1;
    for (let i = 0; i < sb.children.length; i += 1) {
      const child = sb.children[i];
      if (child.className.includes('sfg-row')) {
        index += 1;
        if (index === rowIndex) {
          return child;
        }
      }
    }
    return null;
  }

  _adjustTopForSmallDistance(sb, stayInPlaceIndex, stayInPlaceOffset) {
    if (sb.querySelector) {
      this._adjusting = true;
      const stayInPlaceRow = this._getRowByIndex(sb, stayInPlaceIndex);
      const offsetDelta = stayInPlaceRow.firstElementChild.offsetTop - stayInPlaceOffset;

      const topEdgeDiv = this.renderRoot.getElementById('top-edge');
      let topHeight = topEdgeDiv.scrollHeight - offsetDelta;
      if (topHeight < 0) topHeight = 0;
      topEdgeDiv.style.height = `${topHeight}px`;
      console.log(`Adjusted-small: stayInPlaceIndex: ${stayInPlaceIndex}, offsetDelta: ${offsetDelta}, topOffset:${topHeight} - indx:${this.model.scrollIndex}`);
      return;
    }
    console.log(`Not Adjusted-small: indx:${this.model.scrollIndex}`);
  }

  _scrolled(e) {
    if (this._adjusting) {
      this._adjusting = false;
      return;
    }

    const sb = e.currentTarget;
    if (!sb) {
      return;
    }

    if (!this._ticking) {
      this._ticking = true;
      window.requestAnimationFrame(() => {
        const gridClientHeight = Math.max(sb.clientHeight, this.parentElement.clientHeight);
        this._calculateScrollPos(sb, gridClientHeight);
        this._ticking = false;
      });
    }
  }

  _iterate(rows, scrollIndex, windowRowCount) {
    let limit = scrollIndex + windowRowCount;
    if (limit >= rows.length) {
      limit = rows.length;
    }

    return {
      limit,

      [Symbol.iterator]() {
        this.current = scrollIndex;
        return this;
      },

      next() {
        if (this.current >= 0 && this.current < this.limit) {
          const item = rows[this.current];
          this.current += 1;
          return { done: false, value: item };
        }
        return { done: true };
      },
    };
  }

  render() {
    const sh = this.model.rows.length * 34;

    if (!this._scrollCalculated) {
      let sb = this.renderRoot.getElementById('grid');
      if (!sb) {
        sb = {
          scrollTop: 0,
          scrollHeight: sh,
          clientHeight: this.parentElement.clientHeight,
          children: [],
        };
      }
      const gridClientHeight = Math.max(sb.clientHeight, this.parentElement.clientHeight);
      this._calculateScrollPos(sb, gridClientHeight);
    }

    console.log(`Rendering: indx:${this.model.scrollIndex}`);

    const rowIterator = this._iterate(this.model.rows, this.model.scrollIndex, this.model.scrollCount);

    // alternating row background should be based on absolute row index, not row index in view window
    const alternator = this.model.scrollIndex % 2 === 0 ? '2n' : '2n+1';

    const result = html`
      <!-- <link rel="stylesheet" href="./css/tailwind.css"> -->
      <style>
        :host {
          --column-defs: ${this.gridTemplateColumns()};
          --grid-height: calc(100vh - 45px);
          --grid-width: calc(100% - 0px);
          --scroll-height: ${sh}px;
        }
        /* alternating row background color */
        .sfg-row:nth-child(${alternator}) > div {
            background-color: #f7f7f7;
        }
      </style>
      <div id="gridContainer">
        <div id="grid" class="sfg-container" @scroll="${this._scrolled}">
          <div class="sfg-header-row">
            ${this.headerTemplate()}
          </div>
          <div class="stretcher">&nbsp;</div>
          <div id="top-edge" style="grid-column:1/-1"></div>
          ${repeat(
            rowIterator,
            item => item.chkId,
            (item, indx) => html`
              <div class="sfg-row">
                ${this.rowTemplate(item, indx)}
              </div>
            `
          )}
          <div id="bottom-edge" style="grid-column:1/-1"></div>
        </div>
      </div>
    `;
    return result;
  }
}

window.customElements.define('my-grid', MyGrid);
