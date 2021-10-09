export function* emptyIterator() {
  //
}

window.uft8Decoder = window.uft8Decoder || new TextDecoder();

export function b64DecodeUnicode(base64) {
  const binStr = window.atob(base64);
  const binView = new Uint8Array(binStr.length);
  for (let idx = 0; idx < binView.length; idx += 1) {
    binView[idx] = binStr.charCodeAt(idx);
  }
  return window.uft8Decoder.decode(binView.buffer);
}

// LINQ-like Single() for iterators
export function first(iterator) {
  const iterated = iterator[Symbol.iterator]().next();
  if (!iterated.done) return iterated.value;
  return null;
}

// returns the closest item to the comparand, with preference for the previous item;
// requires that none of the items are undefined!
export function closest(iterator, comparand) {
  let result;
  let found = false;

  for (const item of iterator) {
    if (item === comparand) {
      if (result) break;

      found = true;
      continue;
    }

    if (found) { // take the first key we get
      result = item;
      break;
    }

    result = item;
  }

  return result;
}

// const dateFormat = new Intl.DateTimeFormat('default', { dateStyle: 'short', timeStyle: 'short' });
export const dateFormat = new Intl.DateTimeFormat('default', {
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: 'numeric',
  minute: 'numeric',
  second: 'numeric',
  milli: 'numeric'
});

// this performs a deep (resursive) clone of a Javascript Array, Map, Set, Date or Object;
// other element or property types are assumed to be primitive types like Number or Boolean;
// it should be sufficient for data transfer objects and view models
export function clone(source) {
  if (source instanceof Array) {
    const tgtArray = new Array(source.length);
    for (let indx = 0; indx < source.length; indx += 1) {
      tgtArray[indx] = clone(source[indx]);
    }
    return tgtArray;
  }
  if (source instanceof Map) {
    const tgtMap = new Map();
    for (const [key, value] of source) {
      tgtMap.set(clone(key), clone(value));
    }
    return tgtMap;
  }
  if (source instanceof Set) {
    const tgtSet = new Set();
    for (const element of source) {
      tgtSet.add(clone(element));
    }
    return tgtSet;
  }
  if (source instanceof String) {
    return source.slice(0);
  }
  if (source instanceof Date) {
    return new Date(+source);
  }
  if (source instanceof Object) {
    const target = {};
    // eslint-disable-next-line guard-for-in
    for (const key in source) {
      // Use getOwnPropertyDescriptor instead of source[key] to avoid triggering setter/getter.
      const descriptor = Object.getOwnPropertyDescriptor(source, key);
      const clonedValue = clone(descriptor.value);
      descriptor.value = clonedValue;
      Object.defineProperty(target, key, descriptor);
    }
    const prototype = Reflect.getPrototypeOf(source);
    Reflect.setPrototypeOf(target, prototype);
    return target;
  }
  // assume everything else is a primitive type (number, boolean, ...)
  return source;
}

// this compares the target's properties to the same properties in source;
// extra properties in source objects are ignored, but not in Arrays, Maps or Sets
export function targetEquals(target, source) {
  if (target instanceof Array) {
    if (source instanceof Array && source.length === target.length) {
      for (let indx = 0; indx < target.length; indx += 1) {
        if (!targetEquals(target[indx], source[indx])) {
          return false;
        }
      }
    } else {
      return false;
    }
    return true;
  }
  if (target instanceof Map) {
    if (source instanceof Map && source.size === target.size) {
      for (const [key, value] of target) {
        if (!targetEquals(value, source.get(key))) {
          return false;
        }
      }
    } else {
      return false;
    }
    return true;
  }
  if (target instanceof Set) {
    if (source instanceof Set && source.size === target.size) {
      for (const element of target) {
        if (!source.has(element)) {
          return false;
        }
      }
    } else {
      return false;
    }
    return true;
  }
  if (target instanceof Date) {
    if (source instanceof Date) {
      if (Number(target) !== Number(source)) {
        return false;
      }
    }
    return false;
  }
  if (target instanceof Object) {
    // eslint-disable-next-line guard-for-in
    for (const key in target) {
      if (!targetEquals(target[key], source[key])) {
        return false;
      }
    }
    return true;
  }
  // compare as primitive types
  return target === source;
}

// this assigns existing target object properties from source properties of the same name;
// property types are not matched, that is for instance, an integer could be assigned to a string;
// Note: this are straight assignments, no cloning is performed, so be careful about modifying
// reference type properties on the source after the assignments are done;
export function setTargetProperties(target, source) {
  // eslint-disable-next-line guard-for-in
  for (const key in target) {
    const descriptor = Object.getOwnPropertyDescriptor(target, key);
    if (typeof descriptor === 'undefined') continue;
    // Use getOwnPropertyDescriptor instead of source[key] to avoid triggering setter/getter.
    const sourceDescriptor = Object.getOwnPropertyDescriptor(source, key);
    if (typeof sourceDescriptor === 'undefined') continue;

    Object.defineProperty(target, key, sourceDescriptor);
  }
  return target;
}

export const isoDurationRx = /^P(?:\d+Y)?(?:\d+M)?(?:\d+D)?(?:T(?:\d+H)?(?:\d+M)?(?:\d+(?:\.\d+)?S)?)?$/;

export function spliceStr(str, index, count, add) {
  // We cannot pass negative indexes directly to the 2nd slicing operation.
  if (index < 0) {
    index = str.length + index;
    if (index < 0) {
      index = 0;
    }
  }

  return str.slice(0, index) + (add || '') + str.slice(index + count);
}

// Useful for FormData encoding, see https://github.com/mifi/form-encode-object
export function flattenObject(obj, inRet, inPrefix) {
  const ret = inRet || {};
  const prefix = inPrefix || '';
  if (typeof obj === 'object' && obj != null) {
    Object.keys(obj).forEach(key => {
      flattenObject(obj[key], ret, prefix === '' ? key : `${prefix}[${key}]`);
    });
  } else if (prefix !== '') {
    ret[prefix] = obj;
  }

  return ret;
}

export function objectToFormData(obj) {
  const formData = new FormData();
  const flattened = flattenObject(obj);
  Object.keys(flattened)
    .forEach(key => formData.append(key, flattened[key]));

  return formData;
}

// returns Javascript value corresponding to input elements type
export function getFieldValue(input) {
  let val;
  switch (input.type) {
    case 'number':
      val = input.valueAsNumber;
      break;
    case 'date':
      val = input.valueAsDate;
      break;
    case 'checkbox':
      val = input.checked;
      break;
    default:
      val = input.value;
      break;
  }
  return val;
}

export function containerOffsetLeft(container, element) {
  let containerLeft = 0;
  while (element !== null) {
    containerLeft += element.offsetLeft;
    element = element.offsetParent;
    if (element === container) break;
  }
  return containerLeft;
}

export function containerOffsetTop(container, element) {
  let containerTop = 0;
  while (element !== null) {
    containerTop += element.offsetTop;
    element = element.offsetParent;
    if (element === container) break;
  }
  return containerTop;
}

export const html5DialogSupported = typeof HTMLDialogElement === 'function';
