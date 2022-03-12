docker run -dt -p 50301:80 -p 50300:443 ^
    -v "C:\Users\kwaclaw\AppData\Roaming\Microsoft\UserSecrets:/root/.microsoft/usersecrets:ro" ^
	-v "C:\Users\kwaclaw\AppData\Roaming\ASP.NET\Https:/root/.aspnet/https:ro" ^
    -v "C:\Temp\DP-Keys:/root/.aspnet/DataProtection-Keys" ^
	--mount type=bind,src=D:/Work/Private/KdSoft.EtwListener/EtwEvents.AgentManager/certificates/out/,dst=/app/certs/,readonly ^
	-e "Kestrel:Endpoints:Https:Certificate:Path=/app/certs/agent-manager.kd-soft.net.p12" ^
	kdsoftetwagent
    
rem we could use -e "Kestrel:Endpoints:Https:Certificate:Password=xxxxx" instead if user secrets