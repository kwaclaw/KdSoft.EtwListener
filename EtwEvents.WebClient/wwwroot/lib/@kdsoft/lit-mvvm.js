import { render } from '../lit-html.js';
import { unobserve, observe } from '../@nx-js/observer-util/dist/es.es6.js';

/**
@license
Copyright (c) 2019 The Polymer Project Authors. All rights reserved.
This code may only be used under the BSD style license found at
http://polymer.github.io/LICENSE.txt The complete set of authors may be found at
http://polymer.github.io/AUTHORS.txt The complete set of contributors may be
found at http://polymer.github.io/CONTRIBUTORS.txt Code distributed by Google as
part of the polymer project is also subject to an additional IP rights grant
found at http://polymer.github.io/PATENTS.txt
*/
const supportsAdoptingStyleSheets = 'adoptedStyleSheets' in Document.prototype && 'replace' in CSSStyleSheet.prototype;

const constructionToken = Symbol();

class CSSResult {
  constructor(cssText, safeToken) {
    if (safeToken !== constructionToken) {
      throw new Error('CSSResult is not constructable. Use `unsafeCSS` or `css` instead.');
    }
    this.cssText = cssText;
  }

  // Note, this is a getter so that it's lazy. In practice, this means
  // stylesheets are not created until the first element instance is made.
  get styleSheet() {
    if (this._styleSheet === undefined) {
      // Note, if `adoptedStyleSheets` is supported then we assume CSSStyleSheet is constructable.
      if (supportsAdoptingStyleSheets) {
        this._styleSheet = new CSSStyleSheet();
        this._styleSheet.replaceSync(this.cssText);
      } else {
        this._styleSheet = null;
      }
    }
    return this._styleSheet;
  }

  toString() {
    return this.cssText;
  }
}

/**
 * Wrap a value for interpolation in a css tagged template literal.
 *
 * This is unsafe because untrusted CSS text can be used to phone home
 * or exfiltrate data to an attacker controlled site. Take care to only use
 * this with trusted input.
 */
const unsafeCSS = (value) => {
  return new CSSResult(String(value), constructionToken);
};

const textFromCSSResult = (value) => {
  if (value instanceof CSSResult) {
    return value.cssText;
  } else if (typeof value === 'number') {
    return value;
  } else {
    throw new Error(`Value passed to 'css' function must be a 'css' function result: ${value}. Use 'unsafeCSS' to pass non-literal values, but take care to ensure page security.`);
  }
};

/**
 * Template tag which which can be used with LitElement's `style` property to
 * set element styles. For security reasons, only literal string values may be
 * used. To incorporate non-literal values `unsafeCSS` may be used inside a
 * template string part.
 */
const css = (strings, ...values) => {
  const cssText = values.reduce((acc, v, idx) => acc + textFromCSSResult(v) + strings[idx + 1], strings[0]);
  return new CSSResult(cssText, constructionToken);
};

/* eslint-disable class-methods-use-this */

/**
 * When using Closure Compiler, JSCompiler_renameProperty(property, object) is
 * replaced at compile time by the munged name for object[property]. We cannot
 * alias this function, so we have to use a small shim that has the same
 * behavior when not compiling.
 */
// eslint-disable-next-line no-unused-vars
window.JSCompiler_renameProperty = (prop, _obj) => prop;

/**
 * Sentinel value used to avoid calling lit-html's render function when
 * subclasses do not implement `render`
 */
const renderNotImplemented = {};

class LitBaseElement extends HTMLElement {
  // only called if there is an attributeChangedCallback() defined;
  // we piggy back on this getter to run finalize() to ensure finalize() is run
  static get observedAttributes() {
    return [];
  }

  /**
   * Return the array of styles to apply to the element.
   * Override this method to integrate into a style management system.
   *
   * @nocollapse
   */
  static getStyles() {
    return this.styles;
  }

  /** @nocollapse */
  static _getUniqueStyles() {
    // Only gather styles once per class
    if (Object.prototype.hasOwnProperty.call(this, window.JSCompiler_renameProperty('_styles', this))) {
      return;
    }

    // Take care not to call `this.getStyles()` multiple times since this
    // generates new CSSResults each time.
    // TODO(sorvell): Since we do not cache CSSResults by input, any
    // shared styles will generate new stylesheet objects, which is wasteful.
    // This should be addressed when a browser ships constructable
    // stylesheets.
    const userStyles = this.getStyles();
    if (Array.isArray(userStyles)) {
      // De-duplicate styles preserving the _last_ instance in the set.
      // This is a performance optimization to avoid duplicated styles that can
      // occur especially when composing via subclassing.
      // The last item is kept to try to preserve the cascade order with the
      // assumption that it's most important that last added styles override
      // previous styles.
      const addStyles = (stylesToAdd, styleSet) => stylesToAdd.reduceRight(
        // Note: On IE set.add() does not return the set
        // Note: grouping expression returns last value: '(set.add(styles), set)' returns 'set'
        (set, style) => (Array.isArray(style) ? addStyles(style, set) : (set.add(style), set)),
        styleSet
      );
      // Array.from does not work on Set in IE, otherwise return
      // Array.from(addStyles(userStyles, new Set<CSSResult>())).reverse()
      const set = addStyles(userStyles, new Set());
      const styles = [];
      set.forEach(v => styles.unshift(v));
      this._styles = styles;
    } else {
      this._styles = userStyles === undefined ? [] : [userStyles];
    }

    // Ensure that there are no invalid CSSStyleSheet instances here. They are
    // invalid in two conditions.
    // (1) the sheet is non-constructible (`sheet` of a HTMLStyleElement), but
    //     this is impossible to check except via .replaceSync or use
    // (2) the ShadyCSS polyfill is enabled (:. supportsAdoptingStyleSheets is
    //     false)
    this._styles = this._styles.map(s => {
      if (s instanceof CSSStyleSheet && !supportsAdoptingStyleSheets) {
        // Flatten the cssText from the passed constructible stylesheet (or
        // undetectable non-constructible stylesheet). The user might have
        // expected to update their stylesheets over time, but the alternative
        // is a crash.
        const cssText = Array.prototype.slice.call(s.cssRules)
          .reduce((css, rule) => css + rule.cssText, '');
        return unsafeCSS(cssText);
      }
      return s;
    });
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
    this.constructor._getUniqueStyles();
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
      this.renderRoot.adoptedStyleSheets = styles.map(s => (s instanceof CSSStyleSheet ? s : s.styleSheet));
    } else {
      // This must be done after rendering so the actual style insertion is done in `update`.
      this._needsShimAdoptedStyleSheets = true;
    }
  }

  /**
   * Calls `render` to render DOM via lit-html.
   * This is what should be called by 'observable' implementations.
   */
  _doRender() {
    if (this.shouldRender()) {
      if (!this._firstRendered) {
        this.beforeFirstRender();
      }

      const templateResult = this.render();
      if (templateResult !== renderNotImplemented) {
        render(templateResult, this.renderRoot, {
          scopeName: this.localName,
          eventContext: this,
        });
      }

      // When native Shadow DOM is used but adoptedStyles are not supported,
      // insert styling after rendering to ensure adoptedStyles have highest priority.
      if (this._needsShimAdoptedStyleSheets) {
        this._needsShimAdoptedStyleSheets = false;
        this.constructor._styles.forEach(s => {
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

  _initialRender() {
    this._firstRendered = false;
    this._doRender();
  }

  // this handler must be defined to trigger the necessary call to get observedAttributes() !!!
  attributeChangedCallback(name, oldval, newval) {
    //
  }

  connectedCallback() {
    this._initialRender();
  }

  disconnectedCallback() {
    //
  }

  shouldRender() {
    return true;
  }

  beforeFirstRender() { }

  render() {
    return renderNotImplemented;
  }

  firstRendered() { }

  rendered() { }
}

/* eslint-disable lines-between-class-members */

const _model = new WeakMap();
const _scheduler = new WeakMap();

class LitMvvmElement extends LitBaseElement {
  get model() { return _model.get(this); }
  set model(value) {
    const oldModel = this.model;
    _model.set(this, value);
    // need to re-initialize rendering for a new model when we are already connected;
    // old observers are now useless, and connectedCallback might not get called anymore
    if (oldModel !== value && this.isConnected) {
      this._initialRender();
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
  _setupObserver() {
    if (this._observer) {
      unobserve(this._observer);
    }

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
  }

  _initialRender() {
    this._setupObserver();
    // Triggering the initial call to this._doRender(), thus reading observable properties for the first time.
    // NOTE: this is also necessary because the observer will not get re-triggered until the observed
    //       properties are read!!!, that is, until the "get" traps of the proxy are used!!!
    super._initialRender();
  }

  // we call super._doRender() through the observer, thus observing property access
  _doRender() {
    this._observer();
  }

  attributeChangedCallback(name, oldValue, newValue) {
    super.attributeChangedCallback(name, oldValue, newValue);
    if (this._observer) this._doRender();
  }

  // connectedCallback() {
  //   super.connectedCallback();
  // }

  disconnectedCallback() {
    unobserve(this._observer);
  }

  shouldRender() {
    return true;
  }

  // schedule an operation, useful when performing it after layout has happened;
  // typically called from an override of rendered()
  schedule(callback) {
    if (typeof this.scheduler === 'function') {
      this.scheduler(callback);
    } else if (typeof this.scheduler === 'object') {
      this.scheduler.add(callback);
    } else {
      setTimeout(callback, 0);
    }
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

export { BatchScheduler, CSSResult, LitBaseElement, LitMvvmElement, css, supportsAdoptingStyleSheets, unsafeCSS };
