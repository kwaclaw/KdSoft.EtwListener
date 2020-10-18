module.exports = {
  plugins: [
    require('postcss-import')(),
    require('postcss-copy')({
      basePath: ['node_modules'],
      template: '[path]/[name].[ext][query]',
      dest: 'wwwroot/css/assets'
    }),
    require('tailwindcss')({
      theme: {},
      variants: {},
      plugins: [require('@tailwindcss/custom-forms')],
    }),
    require('autoprefixer'),
  ]
}