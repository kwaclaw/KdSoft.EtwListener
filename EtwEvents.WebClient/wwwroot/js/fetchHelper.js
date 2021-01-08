/* eslint guard-for-in: "off" */

import { cloneObject, objectToFormData } from './utils.js';

function fetchNormal(url, options) {
  return fetch(url, options).then(
    async response => {
      if (response.ok) {
        return Promise.resolve(response);
      }

      // build error as a problem details instance
      const error = { title: null, status: response.status };
      const contentType = response.headers.get('Content-Type') || '';
      if (contentType.startsWith('text/')) {
        const txt = await response.text();
        error.title = txt;
      } else if (contentType.startsWith('application/problem+json') || contentType.startsWith('application/json')) {
        const respError = await response.json();
        Object.assign(error, respError);
      } else {
        error.title = response.statusText || `HTTP ${response.status}`;
      }

      return Promise.reject(error);
    },
    error => {
      if (error && error instanceof Error) {
        error = { title: error.message || 'network error' };
      } else if (!error || typeof error !== 'object') {
        error = { title: error || 'network error' };
      }

      return Promise.reject(error);
    }
  );
}

function fetchWithProgress(progress, url, options) {
  progress.start();
  return fetchNormal(url, options).then(
    response => {
      progress.stop();
      return Promise.resolve(response);
    },
    error => {
      progress.stop();
      return Promise.reject(error);
    }
  );
}

// tries to convert response string to response's content type (if text or JSON)
function unpackTextResponse(response) {
  const contentType = response.headers.get('Content-Type');
  if (!contentType) return response;

  if (contentType.startsWith('text/')) {
    return response.text();
  }
  if (contentType.startsWith('application/problem+json') || contentType.startsWith('application/json')) {
    return response.json();
  }
  return response;
}

const defaultOptions = {
  credentials: 'same-origin',
  headers: { 'X-Requested-With': 'XMLHttpRequest' }
};

// builds URLSearchParams instance suitable for url encoding;
// arrays will be appended as multiple parameters with the same key, useful for query strings
// nested objects, and functions will be skipped;
// we also skip null and undefined properties, because URLSearchParams would encode them as "null" or "undefined";
function urlEncode(obj) {
  const result = new URLSearchParams();
  for (const prop in obj) {
    // Use getOwnPropertyDescriptor instead of params[prop] to prevent from triggering setter/getter.
    const descriptor = Object.getOwnPropertyDescriptor(obj, prop);
    const val = descriptor.value;
    // "variable == null" tests for both, null and undefined, because (null == undefined) is true!, but (null === undefined) is false
    if (val == null) {
      // skip
    } else if (val instanceof String) {
      result.append(prop, val);
    } else if (val instanceof Array) {
      for (let i = 0; i < val.length; i += 1) {
        result.append(prop, val[i]);
      }
    } else if (val instanceof Function) {
      // skip
    } else if (val instanceof Object) {
      //skip
    } else {
      result.append(prop, val);
    }
  }
  return result;
}

function buildUrl(route, url, params) {
  return params ? `${route}/${url}?${urlEncode(params)}` : `${route}/${url}`;
}

function buildGetOptions(options, acceptType) {
  const opts = cloneObject({}, defaultOptions);
  if (options) Object.assign(opts, options);
  opts.method = 'GET';
  if (acceptType) Object.assign(opts.headers, { Accept: acceptType });
  return opts;
}

function buildPostOptions(options, contentType) {
  const opts = cloneObject({}, defaultOptions);
  if (options) Object.assign(opts, options);
  opts.method = 'POST';
  if (contentType) Object.assign(opts.headers, { 'Content-Type': contentType });
  return opts;
}

export default class FetchHelper {
  constructor(route, progress) {
    this._route = route;
    this._progress = progress;
    if (progress) {
      this._fetchCall = (url, options) => fetchWithProgress(progress, url, options);
    } else {
      this._fetchCall = (url, options) => fetchNormal(url, options);
    }
  }

  get route() { return this._route; }

  get progress() { return this._progress; }

  get fetchCall() { return this._fetchCall; }

  // virtual, override if desired
  handleDefault(error) { }

  // can be used to return a clone of the current instance with the overriddent value of progress, override in subclass
  withProgress(progress) {
    if (this.progress === progress) return this;
    return new FetchHelper(this.route, progress);
  }

  // generic GET
  // params: query string parameters, must be an object with simple or array property values, nested objects are not supported
  // options: fetch options - see note above class declaration
  // returns promise
  get(url, params, options) {
    return this.fetchCall(buildUrl(this.route, url, params), buildGetOptions(options)).then(unpackTextResponse);
  }

  // GETs a string
  // params: query string parameters, must be an object with simple or array property values, nested objects are not supported
  // options: fetch options - see note above class declaration
  // returns promise resolving to string
  getText(url, params, options) {
    options = buildGetOptions(options, 'text/plain');
    return this.fetchCall(buildUrl(this.route, url, params), options).then(response => response.text());
  }

  // GETs a JSON object
  // params: query string parameters, must be an object with simple or array property values, nested objects are not supported
  // options: fetch options - see note above class declaration
  // returns promise resolving to JSON object
  getJson(url, params, options) {
    options = buildGetOptions(options, 'application/json');
    return this.fetchCall(buildUrl(this.route, url, params), options)
      .then(response => response.json())
      // seems response.json() sometimes returns a string instead
      .then(json => {
        if (typeof json === 'string') {
          return Promise.resolve(JSON.parse(json));
        }
        return Promise.resolve(json);
      });
  }

  // GETs a string, expecting it to be HTML
  // params: query string parameters, must be an object with simple or array property values, nested objects are not supported
  // options: fetch options - see note above class declaration
  // returns promise resolving to string
  getHtml(url, params, options) {
    options = buildGetOptions(options, 'text/html');
    return this.fetchCall(buildUrl(this.route, url, params), options).then(response => response.text());
  }

  // postXXX methods specify a content type to be sent. The data type to be
  // returned is not specified, but inferred from the MIME type of the response.
  // when possible, the result returned is resolved to a string or JSON promise

  // generic POST
  // params: query string parameters, must be an object with simple or array property values, nested objects are not supported
  // data: passed as body of POST request
  // options: fetch options - see note above class declaration
  // returns promise resolving to string or JSON object
  post(url, params, data, options) {
    options = buildPostOptions(options);
    options.body = data;
    return this.fetchCall(buildUrl(this.route, url, params), options).then(unpackTextResponse);
  }

  // POST with body as string
  // params: query string parameters, must be an object with simple or array property values, nested objects are not supported
  // data: passed as body with content type 'text/plain; charset=utf-8'
  // options: fetch options - see note above class declaration
  // returns promise resolving to string or JSON object
  postText(url, params, data, options) {
    options = buildPostOptions(options, 'text/plain; charset=utf-8');
    options.body = data;
    return this.fetchCall(buildUrl(this.route, url, params), options).then(unpackTextResponse);
  }

  // POST with body as JSON string
  // params: query string parameters, must be an object with simple or array property values, nested objects are not supported
  // data: must be object, passed as JSON string body with content type 'application/json; charset=utf-8'
  // options: fetch options - see note above class declaration
  // returns promise resolving to string or JSON object
  postJson(url, params, data, options) {
    options = buildPostOptions(options, 'application/json; charset=utf-8');
    options.body = JSON.stringify(data);
    return this.fetchCall(buildUrl(this.route, url, params), options).then(unpackTextResponse);
  }

  // see https://stackoverflow.com/questions/46640024/how-do-i-post-form-data-with-fetch-api

  // POST with body as URL encoded form, will work with multiple *simple* type arguments in the Asp.Net Core action method
  // params: query string parameters, must be an object with simple or array property values, nested objects are not supported
  // data: must be *flat* object (no nested object properties), passed as content type 'application/x-www-form-urlencoded; charset=utf-8'
  // options: fetch options - see note above class declaration
  // returns promise resolving to string or JSON object
  postForm(url, params, data, options) {
    options = buildPostOptions(options, 'application/x-www-form-urlencoded; charset=utf-8');
    // this skips null and undefined properties, because URLSearchParams would encode them as "null" or "undefined";
    options.body = urlEncode(data);
    return this.fetchCall(buildUrl(this.route, url, params), options).then(unpackTextResponse);
  }

  // POST with body as FormData object, will work with multiple *complex* type arguments in the Asp.Net Core action method
  // params: query string parameters, must be an object with simple or array property values, nested objects are not supported
  // data: should be a FormData instance, will try to convert a Javascript object to FormData using objectToFormData()
  // options: fetch options - see note above class declaration
  //          Note: must *not* have a content-type header for formMultipart
  // returns promise resolving to string or JSON object
  postFormMultipart(url, params, data, options) {
    options = buildPostOptions(options);
    if (data instanceof FormData) {
      options.body = data;
    } else if (data !== null) {
      options.body = objectToFormData(data);
    }
    return this.fetchCall(buildUrl(this.route, url, params), options).then(unpackTextResponse);
  }
}
