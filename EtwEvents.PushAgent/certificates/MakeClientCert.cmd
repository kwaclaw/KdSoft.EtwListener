@echo off
Setlocal enabledelayedexpansion
:: switch to location of this script
pushd "%~dp0"

:: Prerequisite: OpenSSL >= 3.0 must be installed - see https://slproweb.com/products/Win32OpenSSL.html

:: Example for Agent: MakeClientCert.cmd -name my-etw-site -email karl@kd-soft.net -extra "role=etw-agent"
:: Example for User: MakeClientCert.cmd -name "Karl Waclawek" -email karl@waclawek.net -extra "role=etw-manager"
:: Example for Admin+User: MakeClientCert.cmd -name "Karl Waclawek" -email karl@waclawek.net -extra "role=etw-manager+role=etw-admin"

:: MODIFY FOR YOUR SCENARIO
set config_file=client.cnf                                 
set ca_sign_file=Kd-Soft.crt
set ca_key_file=Kd-Soft.key
set base_distinguished_name=/C=CA/ST=ON/L=Oshawa/O=Kd-Soft


:: Common Name (CN)
set -name=
:: email address
set -email=
:: extra DN component, to be added at end of DN
:: should be in double quotes
set -extra=

:: we use this loop instead of 'for %%a in (%*) do' because for..in ignores arguments with an '*'
:loop
  :: %~1 removes surrounding quotes from first parameter
  set arg=%~1
  call :checkarg %arg%
  if not "%val%"=="" (call set "%param%=%val%") 
  shift
if not "%~1"=="" goto loop
goto :after

:checkarg
if /i [%arg:~0,1%] == [-] (
    set param=%arg%
    call set "val="
) else (
    set val=%arg%
)
exit /b

:after
echo Command Line Parameters
echo -name=%-name%
echo -email=%-email%
echo -extra=%-extra%


if ["%-name%"] == [""] (set -name=client)
if ["%-email%"] == [""] (set -email=client@kd-soft.net)
if ["%-extra%"] == [""] ( cd. ) else (set -extra=/%-extra%)

set cn=CN=%-name%
set em=/emailAddress=%-email%
set dn=%base_distinguished_name%/%cn%%em%%-extra%

mkdir tmp 2>nul
mkdir out 2>nul


@echo generate client key
:: We would like to use elliptic curve cryptography, but using a certificate for data protection limits us to use RSA
:: see https://docs.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-encryption-at-rest?view=aspnetcore-6.0
:: openssl ecparam -out "tmp/client.key" -name secp384r1 -genkey
openssl genrsa -out "tmp/client.key" 2048
if %ERRORLEVEL% NEQ 0 (Exit /b)

@echo generate CSR
openssl req -new -key "tmp/client.key" -out "tmp/client.csr" -config "%config_file%" -subj "%dn%"
if %ERRORLEVEL% NEQ 0 (Exit /b)
    
@echo create and sign the client certificate    
openssl x509 -req -sha256 -days 1095 -in "tmp/client.csr" -CA "%ca_sign_file%" -CAkey "%ca_key_file%" ^
    -CAcreateserial -out "tmp/client.crt" -copy_extensions copy
if %ERRORLEVEL% NEQ 0 (Exit /b)

@echo export to pkcs12
:: SHA256 signed certificates cannot be imported on WinServer 2016 / Win10 <= 1703, use TripleDES-SHA1 instead
openssl pkcs12 -export -in "tmp/client.crt" -inkey "tmp/client.key" -macalg SHA1 -keypbe PBE-SHA1-3DES -certpbe PBE-SHA1-3DES -out "out/%-name%.p12"
:: openssl pkcs12 -export -in "tmp/client.crt" -inkey "tmp/client.key" -out "out/%-name%.p12"

popd    
pause