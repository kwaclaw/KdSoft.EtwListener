SET sinkDir=%1
SET targetDir=%2
SET projectDir=%3

IF NOT EXIST %sinkDir% MKDIR %sinkDir%

COPY /Y %targetDir%* %sinkDir%
XCOPY /Y /E /I %projectDir%config %sinkDir%config
