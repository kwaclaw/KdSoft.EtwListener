/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['views/*.{html,js}', 'components/*.{html,js}', 'eventSinks/**/*.{html,js}'],
  theme: {
    extend: {},
  },
  plugins: [
    require('@tailwindcss/forms'),
  ],
}
