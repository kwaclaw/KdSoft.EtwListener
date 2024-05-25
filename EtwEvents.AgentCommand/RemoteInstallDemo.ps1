# INSTALLING AGENT ON REMOTE HOST

# Create PowerShell session on remote host
# Note: -UseSSL is only necessary for using WinRM with standalone hosts (and is hard to configure on the host)
$session = New-PSSession -ComputerName <remote host> [-Credential <admin user name>] [-UseSSL]

# Copy installer to remote host
Copy-Item -Path "<local directory>\EtwEvents.PushAgent.Setup.msi" -Destination -path "<remote directory>\EtwEvents.PushAgent.Setup.msi" -ToSession $session

# from within that remote session install the agent
Invoke-Command -Session $session -ScriptBlock { msiexec /i c:\temp\EtwEvents.PushAgent.Setup.msi /quiet /log c:\temp\etw-agent.log }

# Exit remote session
Remove-PSSession -Session $session

# CONFIGURING AGENT ON REMOTE HOST
# Switch to directory where the agent command line tool can be found, then run any of the following commands

# Example for setting control options (must be pre-configured in appsettings.json)
KdSoft.EtwEvents.AgentCommand.exe --config="appsettings.json" --host <remote host> --cmd set-control

# Example for installing a new client certificate, the site name must be unique (certificate will be created, email is optional)
KdSoft.EtwEvents.AgentCommand.exe --host <remote host> --cmd new-cert --site-name test-site-1 [--site-email <contact email>]

# Example for setting site options (configure options in agent manager and download using "Export as Command")
# Note: the options file does not have to include a complete set of options, one can, for instance, just set the event sinks
KdSoft.EtwEvents.AgentCommand.exe --host <remote host> --cmd set-options --site-options="D:\\Downloads\\TestSite1.options-command.json\"

# Example for starting the agent
KdSoft.EtwEvents.AgentCommand.exe --host <remote host> --cmd start

