pushd ..

@echo off

:: prompt for certificate password
set /p certPwd=Server Certificate Password:
cls

:: start production container
@echo Starting Container KdSoft_ETW_AgentManager
docker run -dt -p 50301:80 -p 50300:443 ^
    --mount type=bind,src=c:/temp/etw-manager/keys,dst=/root/.aspnet/DataProtection-Keys/ ^
    --mount type=bind,src=c:/temp/etw-manager/certs,dst=/app/certs/,readonly ^
    --mount type=bind,src=c:/temp/etw-manager/Logs,dst=/app/Logs/ ^
    -e "Kestrel:Endpoints:Https:Certificate:Path=/app/certs/agent-manager.kd-soft.net.p12" ^
    -e "Kestrel:Endpoints:Https:Certificate:Password=%certPwd%" ^
    -e "ASPNETCORE_ENVIRONMENT=Production" ^
    --name "KdSoft_ETW_AgentManager" ^
    waclawek/kdsoft-etw-agent-manager:1.3.0
::  if a network is specified, use the option --network=XXX

:: clear certificate password
set certPwd=

:: install root certificate
@echo Installing root certificate C:/Temp/etw-manager/certs/Kd-Soft_Root_CA.crt and Kd-Soft_ETW-Signing_CA.crt
docker cp "C:/Temp/etw-manager/certs/Kd-Soft_Root_CA.crt" KdSoft_ETW_AgentManager:"/usr/local/share/ca-certificates"
docker cp "C:/Temp/etw-manager/certs/Kd-Soft_Test-Signing_CA.crt" KdSoft_ETW_AgentManager:"/usr/local/share/ca-certificates"
docker exec -dt -w "/usr/local/share/ca-certificates" KdSoft_ETW_AgentManager update-ca-certificates

popd
