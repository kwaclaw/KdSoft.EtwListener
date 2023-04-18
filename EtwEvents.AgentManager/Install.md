## Prepare Install Package

- Rebuild all EventSink projects (or simply the entire Solution) in Release mode.
  - This will also run a prebuild event `wwwroot`, which performs an NPM install followed by `npm run build` and `npm run prepare-release`.
- Open project EtwEvents.AgentManager and publish it using either the Platform or the SelfContained profile.
- If using the Platform profile, it is a requirement that the target system has the target framework (specified in the profile) installed.

## Install Package

- After preparing the install package, copy the project folder `deploy` to a (temporary) location on the target system.
  - It is recommended to distribute the `deploy` folder as a zip archive.
- If applicable, check that the publish profile's target framework is installed.
- On a new installation
  - Edit the included `appsettings.Local.json` according to the local requirements (see [Local Configuration](#local-configuration) below).
  - Edit the included `authorization.json` according to the local requirements (see [Local Configuration](#local-configuration) below).
- On an existing installation:
  - Take note of the current install directory, if it needs to stay the same
  - Update the currently installed `appsettings.Local.json` file if changes are desired.
- Check that the proper server certificate for the agent manager is copied to the deploy directory.
  - It must include the private key, and it must be a PKCS12 encoded file with the ".p12" file extension.
  - It must be signed by a root/intermediate certificate accessible to the AgentManager web site.
    - If custom root/intermediate certificates need to be installed as well, then they must be copied to the deploy directory too.
    - They must **not** contain the private key, and their file extension must be ".cer".
  - It can be installed by dragging it onto `tools\InstallServerPfx.cmd`, but it will be auto-detected and installed by the installer anyway.
  - The settings in `appsettings[.Local].json` must match the certificate, but that is handled by the installer.

- Finally, run `InstallService.cmd as administrator (it will prompt for elevation if necessary):
  - The script will prompt for the `<target directory>`.
  - On an existing installation the `<target directory>` should probably match the existing install directory.
  - This will install the application as a windows service called "Etw AgentManager".

### Local Configuration

- If required, override the settings in `appsettings.json` by editing the file `appsettings.Local.json` in the deploy folder.
- if required, override the settings in `authorization.json` by editing the file `authorization.json` in the deploy folder.
- Both files can be changed at run-time and most changes will not require restarting the service.

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
- If the client certificate does not have the appropriate role, it can be authorized by having its common name listed
  in the AuthorizedCommonNames setting of the AgentManager's `authorization.json` file:
  ```json
  "ClientValidation": {
    // this is only checked when the agent's certificate does not have role=etw-manager
    "AuthorizedCommonNames": [
      "Karl Waclawek",
      "John Doe"
    ]
  },
  "AgentValidation": {
    // this is only checked when the agent's certificate does not have role=etw-pushagent
    "AuthorizedCommonNames": []
  },
  // when specified then we only accept certificates derived from this root certificate
  "RootCertificateThumbprint": "",
  // thumbprints of revoked certificates, applies to both, ClientValidation and AgentValidation
  "RevokedCertificates": [
    "cd91bf6d1f52b76285b5b96abb57381d8d92bfa5"
  ],
  ```
- To create client certificates, check the scripts in the `EtwEvents.PushAgent/certificates` folder.
  - They need to be modified for the individual install scenario.
