
node node_modules/postcss-cli/bin/postcss src/styles/fontawesome/css/all.css -o wwwroot/styles/fontawesome/css/all.css

REM node node_modules/postcss-cli/bin/postcss src/styles/**/*.pcss --dir wwwroot/css --ext css --watch

node node_modules/postcss-cli/bin/postcss src/styles/**/*.pcss --dir wwwroot/css --ext css