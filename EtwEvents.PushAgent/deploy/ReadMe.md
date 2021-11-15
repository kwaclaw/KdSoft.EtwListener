## Prepare Install Package

- Open project EtwEvents.PushAgent and publish it using either the Platform or the SelfContained profile.
- If using the Platform profile, make sure the target system has the target framework (specified in the profile) installed.

## Install Package

- After preparing the install package, copy the project folder "deploy" to a (temporary) location on the target system.
- If applicable, check that the publish profile's target framework is installed.
- On a new installation
  - Define the Site name in "appsettings.Local.json", it must be unique among all sites managed by the same Agent Manager.
    - It can also be specified as command line argument for the service, as in "--Site=My-Site-Name"
  - Install the custom self-signed root certificate by dragging it onto tools\InstallRootCert.cmd (see [Client Authentication](#client-authentication) below).
  - Edit the included "appsettings.Local.json" according to the local requirements (see [Local Configuration](#local-configuration) below).
- On an existing installation:
  - Take note of the current install directory, if it needs to stay the same
  - Update the current "appsettings.Local.json" file if changes are desired.
- Check that the proper client certificate for the site is installed (see [Client Authentication](#client-authentication) below).
  - It must include the private key.
  - It must have the role attribute (OID: 2.5.4.72): "etw-pushagent" .
  - It must be signed by a root certificate accessible to the AgentManager web site.
  - It can be installed by dragging it onto tools\InstallClientPfx.cmd.
  - The settings in "appsettings[.Local].json" must match the certificate (see [Client Authentication](#client-authentication) below).
- Finally, run "CreateService.cmd \<target directory>" as administrator:
  - \<target directory> is optional, it defaults to "C:\EtwEvents.PushAgent", on an existing installation
    it may optionally match the current install directory from above.
  - This will install the application as a windows service called "Etw PushAgent".

### Local Configuration

- Override the settings in appsettings.json by editing the file appsettings.Local.json in the deploy folder.

- The settings in "appsettings.Local.json" can selectively override settings in "appsettings.json" without having to duplicate the entire file.

- This example will only override the Directory in the RollingFile section:
  
  ```json
  "Logging": {
    "RollingFile": {
      "Directory": "../Logs",
    }
  },
  ```

### Client Authentication

We can use a self-signed root certificate, as client authentication certificates are provided by us. 

- The self-signed root certificate must be installed in the "**Local Computer\Trusted Root Certification Authorities**" folder of the local certificate storage. It can be installed by dragging it onto tools\InstallRootCert.cmd.
- The client certificate must be configured to support client authorization.

A useful tool for creating certificates is [XCA](https://www.hohnstaedt.de/xca/).

We specify authorized clients in appsettings.Local.json, in the **Control** section:

```json
  "Control": {
    "Uri": "https://agent-manager.kd-soft.net:5300",
    "ClientCertificate": {
      "Location": "LocalMachine",
      "SubjectCN": "test-site-1"
    }
```
