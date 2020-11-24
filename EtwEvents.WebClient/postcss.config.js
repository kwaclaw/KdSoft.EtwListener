module.exports = {
  plugins: {
    'postcss-import': {},
    'postcss-copy': {
      basePath: ['node_modules'],
      template: '[path]/[name].[ext][query]',
      dest: 'wwwroot/css/assets'
    },
    'tailwindcss': {
      theme: {},
      variants: {},
      plugins: [require('@tailwindcss/forms')]
    },
    'autoprefixer': {},
  }
}