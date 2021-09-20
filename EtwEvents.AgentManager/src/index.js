//

import FetchHelper from './js/fetchHelper.js';
import EtwAppModel from './views/etw-app-model.js';
import './views/etw-app.js';
import GetText from 'gettext.js';

// use "new" so that "this" is defined inside of GetText()
window.i18n = new GetText();

const fetcher = new FetchHelper('./');
fetcher.getJson('de_AT.json')
  .then(json => {
    window.i18n.loadJSON(json, 'messages');
  })
  .catch(console.error);

//i18n.setLocale('de_AT');

const etwApp = document.querySelector('etw-app');
etwApp.model = new EtwAppModel();
