
node node_modules/postcss-cli/bin/postcss src/css/fontawesome/css/all.css -o wwwroot/css/fontawesome/css/all.css

node node_modules/postcss-cli/bin/postcss src/css/**/*.pcss --dir wwwroot/css --ext css
