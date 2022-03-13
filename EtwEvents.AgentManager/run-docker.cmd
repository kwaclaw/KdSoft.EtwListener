pushd ..
docker run -dt -p 50301:80 -p 50300:443 ^
    --mount type=bind,src=c:/temp/etw-manager/keys,dst=/root/.aspnet/DataProtection-Keys/ ^
    --mount type=bind,src=c:/temp/etw-manager/certs,dst=/app/certs/,readonly ^
    -e "Kestrel:Endpoints:Https:Certificate:Path=/app/certs/agent-manager.kd-soft.net.p12" ^
    -e "Kestrel:Endpoints:Https:Certificate:Password=_schroed" ^
    -e "ASPNETCORE_ENVIRONMENT=Production" ^
    --name "KdSoft_ETW_AgentManager" ^
    waclawek/kdsoft-etw-agent-manager:1.0
popd
@echo off
rem we could use -e "Kestrel:Endpoints:Https:Certificate:Password=xxxxx" instead if user secrets does not work
rem or we could use docker secrets
rem     --network=etw-net ^
