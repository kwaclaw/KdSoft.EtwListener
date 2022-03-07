rem @echo off
Setlocal enabledelayedexpansion

rem Example: MakeKdSoftClientCert.cmd -name my-etw-site -email karl@kd-soft.net -extra "role^=etw-pushagent"

rem Common Name (CN), default empty
set -name=
rem email address, default empty
set -email=
rem extra DN component, to be added at end of DN, default empty
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

@echo generate CSR
openssl req -new -key "tmp/client.key" -out "tmp/client.csr" -sha256 -config "kd-soft-client.cnf"
    
@echo create and sign the client certificate    
openssl x509 -req -days 750 -in "tmp/client.csr" -sha256 -CA "Kd-Soft.crt" -CAkey "Kd-Soft.key" ^
    -CAcreateserial -out "tmp/client.crt" -subj "%dn%" -copy_extensions copy
    
@echo export to pkcs12
openssl pkcs12 -export -in "tmp/client.crt" -inkey "tmp/client.key" -out "out/%-name%.p12"
    
pause