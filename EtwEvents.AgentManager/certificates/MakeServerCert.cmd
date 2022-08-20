@echo off
Setlocal enabledelayedexpansion

:: Prerequisite: OpenSSL >= 3.0 must be installed - see https://slproweb.com/products/Win32OpenSSL.html
::               or see https://kb.firedaemon.com/support/solutions/articles/4000121705

:: Example: MakeServerCert.cmd -dns sales.my-company.com -email karl@kd-soft.net

:: Modify for your scenario
set ca_sign_file=Kd-Soft.crt
set ca_key_file=Kd-Soft.key
set base_distinguished_name=/C=CA/ST=ON/L=Oshawa/O=Kd-Soft

:: switch to location of this script
pushd "%~dp0"

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
set dn=%base_distinguished_name%/%cn%%em%

mkdir tmp 2>nul
mkdir out 2>nul

@echo generate server key
openssl ecparam -out "tmp/server.key" -name secp384r1 -genkey
if %ERRORLEVEL% NEQ 0 (Exit /b)

@echo generate CSR
openssl req -new -key "tmp/server.key" -out "tmp/server.csr" -config "server.cnf" -subj "%dn%" -addext "subjectAltName=DNS:%-dns%"
if %ERRORLEVEL% NEQ 0 (Exit /b)
    
@echo create and sign the server certificate    
openssl x509 -req  -sha384 -days 398 -in "tmp/server.csr" -CA "%ca_sign_file%" -CAkey "%ca_key_file%" ^
    -CAcreateserial -out "tmp/server.crt" -copy_extensions copy
if %ERRORLEVEL% NEQ 0 (Exit /b)
    
@echo export to pkcs12
:: sha256 encrypted keys cannot be imported on WinServer 2016 / Win10 <= 1703 with Powershell, use TripleDES-SHA1 instead
:: openssl pkcs12 -export -in "tmp/server.crt" -inkey "tmp/server.key" -out "out/%-dns%.p12"
:: openssl pkcs12 -export -in "tmp/server.crt" -inkey "tmp/server.key" -macalg SHA1 -keypbe PBE-SHA1-3DES -certpbe PBE-SHA1-3DES -out "out/%-dns%.p12"
openssl pkcs12 -export -in "tmp/server.crt" -inkey "tmp/server.key" -out "out/%-dns%.p12"
   
popd
pause