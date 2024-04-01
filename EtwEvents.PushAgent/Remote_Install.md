INSTALL USING REMOTE POWERSHELL SESSION

## Build MSI Installer

- Rebuild solution in Release mode
- Build the WiX installer project `EtwEvents.PushAgent.Setup` in Release mode.
- Locate `EtwEvents.PushAgent.Setup.msi` in the output folder.

## Install MSI Installer

- Open a PowerShell console
- Follow the instructions in [Remote Install Demo](../EtwEvents.AgentCommand/RemoteInstallDemo.ps1).
