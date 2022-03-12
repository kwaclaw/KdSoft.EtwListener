## Installing ETW PushAgent Service

- The installer is shipped as a zip archive, with this file in it.

- In addition you will also require two items:
  
  1) A client certificate, required on new installations, can be re-used on updates.
     - The client certificate must be copied into this directory (once unzipped).
     - The certificate must be a PKCS12 certificate with a `.p12` file extension.
     - It must be a proper client certificate and must have the DN component `role=etw-pushagent`.
     - The installer will use the first matching client certificate it can find in this directory.
  2) The Https URL for the agent manager (which controls the agent service).

- Steps:
  
  1) Unzip archive
  2) copy certificate into unzipped folder (if provided)
  3) Double click InstallService.cmd, follow prompts

### Local Configuration

- Optionally, override the settings in `appsettings.json` by editing the file `appsettings.Local.json` in the this folder.

- The settings in `appsettings.Local.json` can selectively override settings in `appsettings.json` without having to duplicate the entire file.

- This example will only override the Directory in the RollingFile section:
  
  ```json
  "Logging": {
    "RollingFile": {
      "Directory": "../Logs",
    }
  },
  ```
