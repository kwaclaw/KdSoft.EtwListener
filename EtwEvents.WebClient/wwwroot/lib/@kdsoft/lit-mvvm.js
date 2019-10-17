import { html, TemplateResult, render } from '../lit-html.js';
import { observe, unobserve } from '../@nx-js/observer-util.js';

/* eslint-disable class-methods-use-this */

const supportsAdoptingStyleSheets = 'adoptedStyleSheets' in Document.prototype && 'replace' in CSSStyleSheet.prototype;

/**
 * When using Closure Compiler, JSCompiler_renameProperty(property, object) is
 * replaced at compile time by the munged name for object[property]. We cannot
 * alias this function, so we have to use a small shim that has the same
 * behavior when not compiling.
 */
// eslint-disable-next-line no-unused-vars
window.JSCompiler_renameProperty = (prop, _obj) => prop;

function arrayFlat(styles, result = []) {
  for (let i = 0, { length } = styles; i < length; i += 1) {
    const value = styles[i];
    if (Array.isArray(value)) {
      arrayFlat(value, result);
    } else {
      result.push(value);
    }
  }
  return result;
}

const flattenStyles = styles => (styles.flat ? styles.flat(Infinity) : arrayFlat(styles));

class LitBaseElement extends HTMLElement {
  // only called if there is an attributeChangedCallback() defined;
  // we piggy back on this getter to run finalize() to ensure finalize() is run
  static get observedAttributes() {
    this.finalize();
    return [];
  }

  static finalize() {
    // Prepare styling that is stamped at first render time.
    // Styling is built from user provided `styles` or is inherited from the superclass.
    // eslint-disable-next-line no-prototype-builtins
    this._styles = this.hasOwnProperty(window.JSCompiler_renameProperty('styles', this))
      ? this._getUniqueStyles()
      : this._styles || [];
  }

  static _getUniqueStyles() {
    // Take care not to call `this.styles` multiple times since this generates new CSSResults each time.
    // TODO(sorvell): Since we do not cache CSSResults by input, any shared styles will generate
    // new stylesheet objects, which is wasteful.
    // This should be addressed when a browser ships constructable stylesheets.
    const userStyles = this.styles;
    const styles = [];
    if (Array.isArray(userStyles)) {
      const flatStyles = flattenStyles(userStyles);
      // As a performance optimization to avoid duplicated styling that can occur especially when composing
      // via subclassing, de-duplicate styles preserving the last item in the list. The last item is kept to
      // try to preserve cascade order with the assumption that it's most important that last added styles
      // override previous styles.
      const styleSet = flatStyles.reduceRight((set, s) => {
        set.add(s);
        // on IE set.add does not return the set.
        return set;
      }, new Set());
      // Array.from does not work on Set in IE
      styleSet.forEach(v => styles.unshift(v));
    } else if (userStyles) {
      styles.push(userStyles);
    }
    return styles;
  }

  constructor() {
    super();
    this.initialize();
  }

  /**
   * Performs element initialization. By default this calls `createRenderRoot` to create
   * the element `renderRoot` node and captures any pre-set values for registered properties.
   */
  initialize() {
    this.renderRoot = this.createRenderRoot();
    // Note, if renderRoot is not a shadowRoot, styles would/could apply to the element's getRootNode().
    // While this could be done, we're choosing not to support this now since it would require different
    // logic around de-duping.
    if (window.ShadowRoot && this.renderRoot instanceof window.ShadowRoot) {
      this.adoptStyles();
    }
  }

  createRenderRoot() {
    return this.attachShadow({ mode: 'open' });
  }

  /**
   * Applies styling to the element shadowRoot using the `static get styles` property.
   * Styling will apply using `shadowRoot.adoptedStyleSheets` where available and will fallback
   * otherwise. When Shadow DOM is available but `adoptedStyleSheets` is not, styles are
   * appended to the end of the `shadowRoot` to [mimic spec behavior]
   * (https://wicg.github.io/construct-stylesheets/#using-constructed-stylesheets).
   */
  adoptStyles() {
    const styles = this.constructor._styles;
    if (styles.length === 0) {
      return;
    }

    if (supportsAdoptingStyleSheets) {
      this.renderRoot.adoptedStyleSheets = styles.map(s => s.styleSheet);
    } else {
      // This must be done after rendering so the actual style insertion is done in `update`.
      this._needsShimAdoptedStyleSheets = true;
    }
  }

  // this handler must be defined to trigger the necessary call to get observedAttributes() !!!
  attributeChangedCallback(name, oldval, newval) {
    //
  }

  render() {
    return html``;
  }

  shouldRender() {
    return true;
  }

  /**
   * Calls `render` to render DOM via lit-html.
   * This is what should be called by 'observable' implementations.
   */
  _doRender() {
    if (this.shouldRender()) {
      const templateResult = this.render();
      if (templateResult instanceof TemplateResult) {
        render(templateResult, this.shadowRoot, {
          scopeName: this.localName,
          eventContext: this,
        });
      }

      // When native Shadow DOM is used but adoptedStyles are not supported,
      // insert styling after rendering to ensure adoptedStyles have highest priority.
      if (this._needsShimAdoptedStyleSheets) {
        this._needsShimAdoptedStyleSheets = false;
        this.constructor._styles.forEach((s) => {
          const style = document.createElement('style');
          style.textContent = s.cssText;
          this.renderRoot.appendChild(style);
        });
      }

      if (!this._firstRendered) {
        this._firstRendered = true;
        this.firstRendered();
      }

      this.rendered();
    }
  }

  connectedCallback() {
    this._firstRendered = false;
    this._doRender();
  }

  disconnectedCallback() {
    //
  }

  firstRendered() {}

  rendered() {}
}

/* eslint-disable lines-between-class-members */

const _model = new WeakMap();
const _scheduler = new WeakMap();

class LitMvvmElement extends LitBaseElement {
  get model() {
    return _model.get(this);
  }
  set model(value) {
    const oldModel = _model.get(this);
    _model.set(this, value);
    if (oldModel !== value) {
      // queue the reaction for later execution or run it immediately
      this._scheduleRender();
    }
  }

  get scheduler() {
    return _scheduler.get(this);
  }
  set scheduler(value) {
    if (value) _scheduler.set(this, value);
    else _scheduler.set(this, r => r());
  }

  constructor() {
    super();
    _scheduler.set(this, r => r());
  }

  // Setting up observer of view model changes.
  // NOTE: the observer will not get re-triggered until the observed properties are read!!!
  //       that is, until the "get" traps of the proxy are used!!!
  // NOTE: the observer code will need to run synchronously, so that the observer
  //       can detect which properties were used at the end of the call!
  connectedCallback() {
    this._observer = observe(
      () => {
        // super._doRender() reads the relevant view model properties synchronously.
        super._doRender();
      },
      {
        // We dont' want to run the observer right away (to start the observation process),
        // as it is run as part of rendering anyway.
        // Note: the observed model/properties must be defined at the time of first render.
        lazy: true,
        scheduler: this.scheduler,
        /* debugger: console.log */
      }
    );

    // Triggering the initial call to this._doRender(), thus reading observable properties for the first time.
    // NOTE: this is also necessary because the observer will not get re-triggered until the observed
    //       properties are read!!!, that is, until the "get" traps of the proxy are used!!!
    super.connectedCallback();
  }

  // we call super._doRender() through the observer, thus observing property access
  _doRender() {
    this._observer();
  }

  // intended for internal use
  _scheduleRender() {
    if (typeof this.scheduler === 'function') {
      this.scheduler(this._doRender.bind(this));
    } else if (typeof this.scheduler === 'object') {
      this.scheduler.add(this._doRender.bind(this));
    } else {
      this._doRender();
    }
  }

  disconnectedCallback() {
    unobserve(this._observer);
  }

  attributeChangedCallback(name, oldValue, newValue) {
    super.attributeChangedCallback(name, oldValue, newValue);
    // queue the reaction for later execution or run it immediately
    this._scheduleRender();
  }
}

const _interval = new WeakMap();
const _lastRendered = new WeakMap();
const _timerID = new WeakMap();
const _reactions = new WeakMap();

// Scheduler for use with @nx-js/observer-util.
// Runs at most every 'interval' milliseconds.
class BatchScheduler {
  constructor(interval) {
    _interval.set(this, interval);
    _reactions.set(this, new Set());
  }

  get interval() {
    return _interval.get(this);
  }
  get lastRendered() {
    return _lastRendered.get(this);
  }
  get reactions() {
    return _reactions.get(this);
  }

  // pseudo private, should not normally be called directly;
  // returns number of reactions run
  _runReactions() {
    const reactions = this.reactions;
    reactions.forEach((reaction) => {
      try {
        reaction();
      } catch (e) {
        // eslint-disable-next-line no-console
        (console.error || console.log).call(console, e.stack || e);
      }
    });
    const count = reactions.size;
    reactions.clear();

    _timerID.set(this, null);
    _lastRendered.set(this, performance.now());

    return count;
  }

  add(reaction) {
    this.reactions.add(reaction);
    if (_timerID.get(this)) {
      return;
    }

    const delta = performance.now() - this.lastRendered;
    if (delta < this.interval) {
      _timerID.set(this, window.setTimeout(() => this._runReactions(), this.interval - delta));
    } else {
      // queue to be run when idle
      _timerID.set(this, window.setTimeout(() => this._runReactions(), 0));
    }
  }

  delete(reaction) {
    return this.reactions.delete(reaction);
  }
}

export { BatchScheduler, LitBaseElement, LitMvvmElement };
