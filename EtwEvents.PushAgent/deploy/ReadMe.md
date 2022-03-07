## Installing ETW PushAgent Service

- The installer is shipped as a zip archive, with this file in it.

- In addition you will also require two items:
  
  1) A client certificate, especially on new installation. The client certificate must be copied into this directory (once unzipped).
  2) The URL for the agent manager (which controls the agent service).

- Steps:
  
  1) Unzip archive
  2) copy certificate into unzipped folder (if provided)
  3) Double click InstallService.cmd, follow prompts

### Local Configuration

- Optionally, override the settings in appsettings.json by editing the file appsettings.Local.json in the this folder.

- The settings in "appsettings.Local.json" can selectively override settings in "appsettings.json" without having to duplicate the entire file.

- This example will only override the Directory in the RollingFile section:
  
  ```json
  "Logging": {
    "RollingFile": {
      "Directory": "../Logs",
    }
  },
  ```
