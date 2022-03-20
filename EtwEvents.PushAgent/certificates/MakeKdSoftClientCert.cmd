@echo off
Setlocal enabledelayedexpansion

rem Example: MakeKdSoftClientCert.cmd -name my-etw-site -email karl@kd-soft.net -extra "role^=etw-pushagent"

rem Common Name (CN)
set -name=
rem email address
set -email=
rem extra DN component, to be added at end of DN
rem should be in double quotes, any '=' must be escaped as '^='
set -extra=

rem %~1 removes surrounding quotes from first parameter
for %%a in (%*) do (
    call set "%%~1=%%~2"
    shift
)

if [%-name%] == [] (set -name=client)
if [%-email%] == [] (set -email=client@kd-soft.net)
if [%-extra%] == [] ( cd. ) else (set -extra=/%-extra%)

set cn=CN=%-name%
set em=/emailAddress=%-email%
set dn=/C=CA/ST=ON/L=Oshawa/O=Kd-Soft/%cn%%em%%-extra%

@echo generate client key
openssl genrsa -out "tmp/client.key" 4096
if %ERRORLEVEL% NEQ 0 (Exit /b)

@echo generate CSR
openssl req -new -key "tmp/client.key" -out "tmp/client.csr" -config "kd-soft-client.cnf"
if %ERRORLEVEL% NEQ 0 (Exit /b)
    
@echo create and sign the client certificate    
openssl x509 -req -days 750 -in "tmp/client.csr" -CA "Kd-Soft.crt" -CAkey "Kd-Soft.key" ^
    -CAcreateserial -out "tmp/client.crt" -subj "%dn%" -copy_extensions copy
if %ERRORLEVEL% NEQ 0 (Exit /b)

  
@echo export to pkcs12
rem sha256 encrypted keys cannot be imported on WinServer 2016 / Win10 <= 1703, use TripleDES-SHA1 instead
rem openssl pkcs12 -export -in "tmp/client.crt" -inkey "tmp/client.key" -out "out/%-name%.p12"
openssl pkcs12 -export -in "tmp/client.crt" -inkey "tmp/client.key" -macalg SHA1 -keypbe PBE-SHA1-3DES -certpbe PBE-SHA1-3DES -out "out/%-name%.p12"
    
pause