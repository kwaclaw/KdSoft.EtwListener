pushd ..
docker run -dt -p 50301:80 -p 50300:443 ^
	--mount type=bind,src="%APPDATA%\\Microsoft\\UserSecrets\\",dst=/root/.microsoft/usersecrets/,readonly ^
	--mount type=bind,src="%APPDATA%\\ASP.NET\\Https\\",dst=/root/.aspnet/https/,readonly ^
	--mount type=bind,src="C:\\Temp\\DP-Keys\\",dst=/root/.aspnet/DataProtection-Keys/ ^
	--mount type=bind,src=D:/Work/Private/KdSoft.EtwListener/EtwEvents.AgentManager/certificates/out/,dst=/app/certs/,readonly ^
	-e "Kestrel:Endpoints:Https:Certificate:Path=/app/certs/agent-manager.kd-soft.net.p12" ^
    -e "ASPNETCORE_ENVIRONMENT=Production" ^
    --name "KdSoft_ETW_AgentManager" ^
	kdsoftetwagent
popd

rem we could use -e "Kestrel:Endpoints:Https:Certificate:Password=xxxxx" instead if user secrets