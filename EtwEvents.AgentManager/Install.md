## Prepare Install Package

- Open project EtwEvents.AgentManager and publish it using either the Platform or the SelfContained profile.
- If using the Platform profile, it is a requirement that the target system has the target framework (specified in the profile) installed.

## Install Package

- After preparing the install package, copy the project folder `deploy` to a (temporary) location on the target system.
  - It is recommended to distribute the `deploy` folder as a zip archive.
- If applicable, check that the publish profile's target framework is installed.
- On a new installation
  - Edit the included `appsettings.Local.json` according to the local requirements (see [Local Configuration](#local-configuration) below).
- On an existing installation:
  - Take note of the current install directory, if it needs to stay the same
  - Update the current `appsettings.Local.json` file if changes are desired.
- Check that the proper server certificate for the agent manager is copied to the deploy directory.
  - It must include the private key.
  - It must be signed by a root certificate accessible to the AgentManager web site.
  - It can be installed by dragging it onto `tools\InstallServerPfx.cmd`, but it will be auto-detected and installed by the installer anyway.
  - The settings in `appsettings[.Local].json` must match the certificate, but that is handled by the installer.
- Finally, run `InstallService.cmd as administrator (it will prompt for elevation if necessary):
  - The script will prompt for the `<target directory>`.
  - On an existing installation the `<target directory>` should probably match the existing install directory.
  - This will install the application as a windows service called "Etw AgentManager".

### Local Configuration

- Override the settings in `appsettings.json` by editing the file `appsettings.Local.json` in the deploy folder.

- The settings in `appsettings.Local.json` can selectively override settings in `appsettings.json` without having to duplicate the entire file.

- This example will only override the Directory in the RollingFile section:
  
  ```json
  "Logging": {
    "RollingFile": {
      "Directory": "../Logs",
    }
  },
  ```

### Client Authentication

The User and the ETW PushAgent authenticate themselves to the AgentManager using client certificates.
- The client certificate must be configured to support client authorization.
- The client certificate presented by the PushAgent will be authorized if the DN contains role=etw-pushagent.
- The client certificate presented by the User will be authorized if the DN contains role=etw-manager.
- If the client certificate does not have the apprpriate role, it can be authorized by having its common name listed in the AuthorizedCommonNames setting of the AgentManager:
```json
  "ClientValidation": {
    "RootCertificateThumbprint": "d87dce532fb17cabd3804e77d7f344ec4e49c80f",
    // this is only checked when the agent's certificate does not have role=etw-manager
    "AuthorizedCommonNames": [
      "Karl Waclawek",
      "John Doe"
    ]
  },
  "AgentValidation": {
    "RootCertificateThumbprint": "d87dce532fb17cabd3804e77d7f344ec4e49c80f",
    // this is only checked when the agent's certificate does not have role=etw-pushagent
    "AuthorizedCommonNames": []
  },

  ```
