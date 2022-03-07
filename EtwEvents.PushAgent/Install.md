## Prepare Install Package

- Open project EtwEvents.PushAgent and publish it using either the Platform or the SelfContained profile.
- If using the Platform profile, it is a requirement that the target system has the target framework (specified in the profile) installed.

## Install Package

- After preparing the install package, copy the project folder "deploy" to a (temporary) location on the target system.
- If applicable, check that the publish profile's target framework is installed.
- On a new installation
  - Edit the included "appsettings.Local.json" according to the local requirements (see [Local Configuration](#local-configuration) below).
  - It is not necessary to enter agent manager URL or certificate thumbprint, as they can be entered when running InstallService.cmd.
- On an existing installation:
  - Take note of the current install directory, if it needs to stay the same
  - Update the current "appsettings.Local.json" file if changes are desired.
- Check that the proper client certificate for the site is copied to the deploy directory (see [Client Authentication](#client-authentication) below).
  - It must include the private key.
  - It must have the role attribute (OID: 2.5.4.72): "etw-pushagent" .
  - It must be signed by a root certificate accessible to the AgentManager web site.
  - It can be installed by dragging it onto tools\InstallClientPfx.cmd, but it will be auto-detected and installed by the installer anyway.
  - The settings in "appsettings[.Local].json" must match the certificate (see [Client Authentication](#client-authentication) below), 
    but that is handled by the installer.
- Finally, run "InstallService.cmd -installDir <target directory> -url <manager URL>" as administrator (it will prompt for elevation if necessary):
  - <target directory> and <manager URL> are both optional, the script will prompt for them if not provided.
  - On an existing installation the <target directory> should probably match the existing install directory.
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
- We can create client certificates using the script certificates\MakeKdSoftClientCert.cmd.
  - Another useful tool for creating certificates is [XCA](https://www.hohnstaedt.de/xca/)

When a certificate is provided, then the installer will set the certificate's thumprint in appsettings.Local.json, in the **Control** section:

```json
  "Control": {
    "Uri": "https://agent-manager.kd-soft.net:5300",
    "ClientCertificate": {
      "Location": "LocalMachine",
      "SubjectRole": "etw-pushagent",
      "Thumbprint": "XXXX..." (optional)
    }
```
