# copy installer to target host
copy-item -path "<local directory>\EtwEvents.PushAgent.Setup.msi" -Destination "\\<remote host>\c$\windows\temp\EtwEvents.PushAgent.Setup.msi"

# start remote management session, and if not already a domain admin, use admin credentials (admin on remote host)
# Note: -UseSSL is only necessary for using WinRM with standalone hosts (and is hard to configure on the host)
Enter-PSSession -ComputerName <remote host> [-Credential <admin user name>] [-UseSSL]

# from within that remote session install the agent
msiexec /i c:\windows\temp\EtwEvents.PushAgent.Setup.msi /quiet /log c:\windows\temp\etw-agent.log

# exit remote session, switch to directory where the agent command line tool can be found, then run the following commands

# set control options (must be pre-configured in appsettings.json)
KdSoft.EtwEvents.AgentCommand.exe --config="appsettings.json" --host <remote host> --cmd set-control

#  install new client certificate, the site name must be unique (will be created, email is optional)
KdSoft.EtwEvents.AgentCommand.exe --host <remote host> --cmd new-cert --site-name test-site-1 [--site-email <contact email>]

#  set site options (configure and download from agent manager - "Export as Command")
KdSoft.EtwEvents.AgentCommand.exe --host <remote host> --cmd set-options --site-options="D:\\Downloads\\TestSite1.options-command.json\"

# start the agent
KdSoft.EtwEvents.AgentCommand.exe --host <remote host> --cmd start

