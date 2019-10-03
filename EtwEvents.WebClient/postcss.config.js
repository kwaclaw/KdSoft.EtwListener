module.exports = {
  plugins: [
    require('postcss-import')(),
    require('tailwindcss')({
      theme: {},
      variants: {},
      plugins: [require('@tailwindcss/custom-forms')],
    }),
    require('autoprefixer'),
  ]
}