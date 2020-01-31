
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
        error.title = response.statusText;
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

function buildUrl(route, url, params) {
  return params ? `${route}/${url}?${new URLSearchParams(params)}` : `${route}/${url}`;
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

const _route = new WeakMap();
const _progress = new WeakMap();
const _fetchCall = new WeakMap();

export default class FetchHelper {
  constructor(route, progress) {
    _route.set(this, route);
    _progress.set(this, progress);
    if (progress) {
      _fetchCall.set(this, (url, options) => fetchWithProgress(progress, url, options));
    } else {
      _fetchCall.set(this, (url, options) => fetchNormal(url, options));
    }
  }

  get route() { return _route.get(this); }

  get progress() { return _progress.get(this); }

  get fetchCall() { return _fetchCall.get(this); }

  // virtual, override if desired
  handleDefault(error) { }

  // can be used to return a clone of the current instance with the overriddent value of useProgress, override in subclass
  withProgress(progress) {
    if (this.progress === progress) return this;
    return new FetchHelper(this.route, progress);
  }

  // generic GET, params must be an object
  get(url, params, options) {
    return this.fetchCall(buildUrl(this.route, url, params), buildGetOptions(options)).then(unpackTextResponse);
  }

  // params must be an object, returns promise resolving to string
  getText(url, params, options) {
    options = buildGetOptions(options, 'text/plain');
    return this.fetchCall(buildUrl(this.route, url, params), options).then(response => response.text());
  }

  // params must be an object, returns promise resolving to JSON object
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

  // params must be an object, returns promise resolving to string
  getHtml(url, params, options) {
    options = buildGetOptions(options, 'text/html');
    return this.fetchCall(buildUrl(this.route, url, params), options).then(response => response.text());
  }

  // postXXX methods specify a content type to be sent. The data type to be
  // returned is not specified, but inferred from the MIME type of the response.
  // when possible, the result returned is resolved to a string or JSON promise

  // generic POST, params must be an object, tries to resolve response to string or JSON object
  post(url, params, data, options) {
    options = buildPostOptions(options);
    options.body = data;
    return this.fetchCall(buildUrl(this.route, url, params), options).then(unpackTextResponse);
  }

  // params must be an object, data is assumed to be a string, tries to resolve response to string or JSON object
  postText(url, params, data, options) {
    options = buildPostOptions(options, 'text/plain; charset=utf-8');
    options.body = data;
    return this.fetchCall(buildUrl(this.route, url, params), options).then(unpackTextResponse);
  }

  // params and data must be objects, data will be converted to JSON, tries to resolve response to string or JSON object
  postJson(url, params, data, options) {
    options = buildPostOptions(options, 'application/json; charset=utf-8');
    options.body = JSON.stringify(data);
    return this.fetchCall(buildUrl(this.route, url, params), options).then(unpackTextResponse);
  }

  // see https://stackoverflow.com/questions/46640024/how-do-i-post-form-data-with-fetch-api

  // params must be object, data must be *flat* object (no nested object properties),
  // tries to resolve response to string or JSON object;
  // this will work with multiple simple type arguments in the Asp.Net Core action method
  postForm(url, params, data, options) {
    options = buildPostOptions(options, 'application/x-www-form-urlencoded; charset=utf-8');
    // we remove null and undefined properties, because URLSearchParams would encode them as "null" or "undefined";
    // "variable == null" tests for both, null and undefined, because (null == undefined) is true!, but (null === undefined) is false
    for (const prop in data) {
      if (Object.prototype.hasOwnProperty.call(data, prop) && data[prop] == null) {
        delete data[prop];
      }
    }
    const body = new URLSearchParams(data);
    options.body = body;
    return this.fetchCall(buildUrl(this.route, url, params), options).then(unpackTextResponse);
  }

  // params must be an object, data should be a FormData instance, will try to convert if possible;
  // this will work with multiple complex type arguments in the Asp.Net Core action method;
  // must not have a content-type header for formMultipart;
  // tries to resolve response to string or JSON object
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
