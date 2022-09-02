@echo off
Setlocal enabledelayedexpansion
:: switch to location of this script
pushd "%~dp0"

:: Prerequisite: OpenSSL >= 3.0 must be installed - see https://slproweb.com/products/Win32OpenSSL.html

:: Example for Agent: MakeClientCert_smartcard.cmd -name my-etw-site -email karl@kd-soft.net -extra "role=etw-pushagent"
:: Example for User: MakeClientCert_smartcard.cmd -name "Karl Waclawek" -email karl@waclawek.net -extra "role=etw-manager"

:: MODIFY FOR YOUR SCENARIO
set config_file=client_smartcard.cnf                                 
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
:: openssl ecparam -out "tmp/client.key" -name secp384r1 -genkey
:: The PIV standard allows RSA 2048 bit keys, or elliptic curve keys based on curve prime256v1
::  - see https://nvlpubs.nist.gov/nistpubs/specialpublications/nist.sp.800-78-4.pdf
:: However, the Windows Base Smart Card CryptoProvider does not seem to support elliptic curve keys.
:: openssl ecparam -out "tmp/client.key" -name prime256v1 -genkey
openssl genrsa -out "tmp/client.key" 2048
if %ERRORLEVEL% NEQ 0 (Exit /b)

@echo generate CSR
:: OpenSSL 3.0 bug: -addext with long values clears the existing extensions from the config file !!!!!!!!!!!!!!!!!!!!!!!!!!!!
:: openssl req -new -key "tmp/client.key" -out "tmp/client.csr" -config "%config_file%" -subj "%dn%" ^
::     -addext "subjectAltName=otherName:1.3.6.1.4.1.311.20.2.3;UTF8:%-email%"
:: Workaround: we communicate the email through an environemnt variable that is referenced in the config file
set EMAIL_ADDRESS=%-email%
openssl req -new -key "tmp/client.key" -out "tmp/client.csr" -config "%config_file%" -subj "%dn%"
if %ERRORLEVEL% NEQ 0 (Exit /b)
    
@echo create and sign the client certificate
:: The PIV standard does not allow SHA384 for the hash.
:: openssl x509 -req -sha384 -days 1095 -in "tmp/client.csr" -CA "%ca_sign_file%" -CAkey "%ca_key_file%" ^
::     -CAcreateserial -out "tmp/client.crt" -copy_extensions copy
openssl x509 -req -sha256 -days 1095 -in "tmp/client.csr" -CA "%ca_sign_file%" -CAkey "%ca_key_file%" ^
    -CAcreateserial -out "tmp/client.crt" -copy_extensions copy
if %ERRORLEVEL% NEQ 0 (Exit /b)

@echo export to pkcs12
:: SHA256 signed certificates cannot be imported on WinServer 2016 / Win10 <= 1703, use TripleDES-SHA1 instead
:: openssl pkcs12 -export -in "tmp/client.crt" -inkey "tmp/client.key" -macalg SHA1 -keypbe PBE-SHA1-3DES -certpbe PBE-SHA1-3DES -out "out/%-name%.p12"
openssl pkcs12 -export -in "tmp/client.crt" -inkey "tmp/client.key" -out "out/%-name%.p12"

popd    
pause