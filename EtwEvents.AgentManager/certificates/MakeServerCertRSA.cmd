@echo off
Setlocal enabledelayedexpansion
:: switch to location of this script
pushd "%~dp0"

:: Prerequisite: OpenSSL >= 3.0 must be installed - see https://slproweb.com/products/Win32OpenSSL.html
::               or see https://kb.firedaemon.com/support/solutions/articles/4000121705

:: Example: MakeServerCert.cmd -dns sales.my-company.com -email karl@kd-soft.net

:: MODIFY FOR YOUR SCENARIO
set ca_sign_file=Kd-Soft.crt
set ca_key_file=Kd-Soft.key
set base_distinguished_name=/C=CA/ST=ON/L=Oshawa/O=Kd-Soft


:: Common Name (CN) for server, also subjectAltName
set -dns=
:: email address
set -email=

:: we use this loop instead of 'for %%a in (%*) do' because for..in ignores arguments with an '*'
:loop
  set arg=%1
  call :setarg %1
  shift
if not "%~1"=="" goto loop
goto :after

:setarg
if /i [%arg:~0,1%] == [-] (
    set param=%arg%
) else (
    :: %~1 removes surrounding quotes from first parameter
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
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "tmp/server.key"
if %ERRORLEVEL% NEQ 0 (Exit /b)

@echo generate CSR
:: since the -addext switch is broken in some versions of OpenSSL we use an environment variable referenced in server.cnf
set SUBJ_ALT_NAME=DNS:%-dns%
openssl req -new -key "tmp/server.key" -out "tmp/server.csr" -config "server.cnf" -subj "%dn%"
if %ERRORLEVEL% NEQ 0 (Exit /b)
    
@echo create and sign the server certificate    
openssl x509 -req -sha384 -days 398 -in "tmp/server.csr" -CA "%ca_sign_file%" -CAkey "%ca_key_file%" ^
    -CAcreateserial -out "tmp/server.crt" -copy_extensions copy
if %ERRORLEVEL% NEQ 0 (Exit /b)
    
@echo export to pkcs12
:: sha256 encrypted keys cannot be imported on WinServer 2016 / Win10 <= 1703 with Powershell, use TripleDES-SHA1 instead
:: openssl pkcs12 -export -in "tmp/server.crt" -inkey "tmp/server.key" -out "out/%-dns%.p12"
:: openssl pkcs12 -export -in "tmp/server.crt" -inkey "tmp/server.key" -macalg SHA1 -keypbe PBE-SHA1-3DES -certpbe PBE-SHA1-3DES -out "out/%-dns%.p12"

:: replace wildcard character '*' with '_' (need to escape '*' as '**')
set -dns=%-dns:**=_%
openssl pkcs12 -export -in "tmp/server.crt" -inkey "tmp/server.key" -out "out/%-dns%.p12"
   
popd
pause