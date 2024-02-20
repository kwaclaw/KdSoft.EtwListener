## Installing ETW PushAgent Service

- The installer is shipped as a zip archive, with this file in it.

- In addition you will also require:
  
  1) A client certificate, required on new installations, can be re-used on updates.
     - The client certificate must be copied into this directory (once unzipped).
     - The certificate must be a PKCS12 certificate with a `.p12` file extension.
     - One of these applies:
        - Use a client certificate where the subject (DN) includes "role=etw-agent" 
        - Use a client certificate where the Common Name (CN) is listed in the Agent Manager's authorization.json under AgentValidation
     - The installer will import any matching client certificates it can find in this directory.
     - Note: On install, the Control::ClientCertificate::SubjectCN property in appsettings.Local.json will be set to the last imported client certificate's Common Name'.
  2) Any root or intermediate CA certificates required to validate the client certificate.
     - These certificates must have the `.cer` file extension.
  3) The Https URL for the agent manager (which controls the agent service).

- Steps:
  
  1) Unzip archive.
  2) copy certificates into unzipped folder (if provided).
  3) Modify `appsettings.Local.json` if needed.
  4) Double click `InstallService.cmd`, follow prompts.

### Local Configuration

- Optionally, change the settings in `appsettings.Local.json` (in this folder) before running InstallService.cmd.
  - Note: Powershell 5.1 (default version) does not support comments in JSON files.
  - Note: when running the installer to update an already existing install, then the installed configuration files
    will be merged back into the new configuration files before being replaced, to preserve the existing configuration.

- The settings in `appsettings.Local.json` can selectively override settings in `appsettings.json` without having to duplicate the entire file.

- This example will only override the Directory in the RollingFile section:
  
  ```json
  "Logging": {
    "RollingFile": {
      "Directory": "./Logs",
    }
  },
  ```
