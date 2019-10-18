
export function* emptyIterator() {
  //
}

// LINQ-like Single() for iterators
export function first(iterator) {
  const iterated = iterator[Symbol.iterator]().next();
  if (!iterated.done) return iterated.value;
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

    if (found) {  // take the first key we get
      result = item;
      break;
    }

    result = item;
  }

  return result;
}
