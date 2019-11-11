
export function* emptyIterator() {
  //
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

// const noType = 0;
// const arrayType = 1;
// const objectType = 2;

// function typeOf(input) {
//   if (Array.isArray(input)) return arrayType;
//   if (input !== null && input instanceof Object) return objectType;
//   return noType;
// }

// function clone(input) {
//   let output = input;
//   const type = typeOf(input);
//   if (type === arrayType) {
//     output = [];
//     const size = input.length;
//     for (let index = 0; index < size; index += 1) {
//       output[index] = clone(input[index]);
//     }
//   } else if (type === objectType) {
//     output = {};
//     for (const key in input) {
//       if (!Object.prototype.hasOwnProperty.call(input, key)) continue;
//       output[key] = clone(input[key]);
//     }
//   }
//   return output;
// }

// // first argument must be boolean - true for deep clone, false for shallow
// export function mergeObjects(...args) {
//   let result = args[0];
//   const deep = (result === true);
//   const size = arguments.length;
//   if (deep || typeOf(result) !== objectType) result = {};

//   for (let index = 1; index < size; index += 1) {
//     const item = args[index];
//     if (typeOf(item) === objectType) {
//       for (const key in item) {
//         if (!Object.prototype.hasOwnProperty.call(item, key)) continue;
//         result[key] = deep ? clone(item[key]) : item[key];
//       }
//     }
//   }
//   return result;
// }

// export function cloneObject(target, source) {
//   for (const key in source) {
//     // Use getOwnPropertyDescriptor instead of source[key] to prevent from trigering setter/getter.
//     const descriptor = Object.getOwnPropertyDescriptor(source, key);
//     if (descriptor.value instanceof String) {
//       target[key] = new String(descriptor.value);
//     }
//     else if (descriptor.value instanceof Array) {
//       target[key] = cloneObject([], descriptor.value);
//     }
//     else if (descriptor.value instanceof Object) {
//       const prototype = Reflect.getPrototypeOf(descriptor.value);
//       const cloneObj = cloneObject({}, descriptor.value);
//       Reflect.setPrototypeOf(cloneObj, prototype);
//       target[key] = cloneObj;
//     }
//     else {
//       Object.defineProperty(target, key, descriptor);
//     }
//   }
//   const prototype = Reflect.getPrototypeOf(source);
//   Reflect.setPrototypeOf(target, prototype);
//   return target;
// }

// this performs a full clone of a Javascript object or array
export function cloneObject(target, source) {
  // eslint-disable-next-line guard-for-in
  for (const key in source) {
    // Use getOwnPropertyDescriptor instead of source[key] to prevent from triggering setter/getter.
    const descriptor = Object.getOwnPropertyDescriptor(source, key);
    if (descriptor.value instanceof String) {
      target[key] = descriptor.value.slice(0);
    } else if (descriptor.value instanceof Array) {
      target[key] = cloneObject([], descriptor.value);
    } else if (descriptor.value instanceof Object) {
      const prototype = Reflect.getPrototypeOf(descriptor.value);
      const cloneObj = cloneObject({}, descriptor.value);
      Reflect.setPrototypeOf(cloneObj, prototype);
      target[key] = cloneObj;
    } else {
      Object.defineProperty(target, key, descriptor);
    }
  }
  const prototype = Reflect.getPrototypeOf(source);
  Reflect.setPrototypeOf(target, prototype);
  return target;
}

// this assigns existing target object properties from source properties of the same name;
// property types are not matched, that is for instance, an integer could be assigned to a string;
// Note: this are straight assignments, no cloning is performed, so be careful about modifying
// reference type properties on the source after the assignments are done;
export function assignTargetProperties(target, source) {
  // eslint-disable-next-line guard-for-in
  for (const key in target) {
    const descriptor = Object.getOwnPropertyDescriptor(target, key);
    if (typeof descriptor === 'undefined') continue;
    // Use getOwnPropertyDescriptor instead of source[key] to prevent from triggering setter/getter.
    const sourceDescriptor = Object.getOwnPropertyDescriptor(source, key);
    if (typeof sourceDescriptor === 'undefined') continue;

    Object.defineProperty(target, key, sourceDescriptor);
  }
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
