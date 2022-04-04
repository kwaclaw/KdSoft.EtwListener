@echo off
Setlocal enabledelayedexpansion

:: Prerequisite: OpenSSL >= 3.0 must be installed - see https://slproweb.com/products/Win32OpenSSL.html

:: Example: MakeKdSoftServerCert.cmd -dns sales.my-company.com -email karl@kd-soft.net

:: Common Name (CN) for server, also subjectAltName
set -dns=
:: email address
set -email=

:: %~1 removes surrounding quotes from first parameter
for %%a in (%*) do (
    set arg=%%~a
    call :setarg %%a
)
goto :after

:setarg
if /i [%arg:~0,1%] == [-] (
    set param=%arg%
) else (
    call set "%param%=%~1"
)
exit /b

:after
echo Command Line Parameters
echo -dns=%-dns%
echo -email=%-email%


if ["%-dns%"] == [""] (set -dns=server.kd-soft.net)
if ["%-email%"] == [""] (set -email=admin@kd-soft.net)

set cn=CN=%-dns%
set em=/emailAddress=%-email%
set dn=/C=CA/ST=ON/L=Oshawa/O=Kd-Soft/%cn%%em%

@echo generate server key
openssl genrsa -out "tmp/server.key" 4096
if %ERRORLEVEL% NEQ 0 (Exit /b)

@echo generate CSR
openssl req -new -key "tmp/server.key" -out "tmp/server.csr" -config "kd-soft-server.cnf" -addext "subjectAltName=DNS:%-dns%"
if %ERRORLEVEL% NEQ 0 (Exit /b)
    
@echo create and sign the server certificate    
openssl x509 -req -days 398 -in "tmp/server.csr" -CA "Kd-Soft.crt" -CAkey "Kd-Soft.key" ^
    -CAcreateserial -out "tmp/server.crt" -subj "%dn%" -copy_extensions copy
if %ERRORLEVEL% NEQ 0 (Exit /b)
    
@echo export to pkcs12
:: sha256 encrypted keys cannot be imported on WinServer 2016 / Win10 <= 1703, use TripleDES-SHA1 instead
:: openssl pkcs12 -export -in "tmp/server.crt" -inkey "tmp/server.key" -out "out/%-dns%.p12"
openssl pkcs12 -export -in "tmp/server.crt" -inkey "tmp/server.key" -macalg SHA1 -keypbe PBE-SHA1-3DES -certpbe PBE-SHA1-3DES -out "out/%-dns%.p12"
   
pause