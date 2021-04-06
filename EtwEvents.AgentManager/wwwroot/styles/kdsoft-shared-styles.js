import { html } from '../lib/lit-html.js';
import styleLinks from './kdsoft-style-links.js';

export default html`
  <link rel="stylesheet" type="text/css" href=${styleLinks.tailwind} />
  <link rel="stylesheet" type="text/css" href=${styleLinks.fontawesome} />
`;
