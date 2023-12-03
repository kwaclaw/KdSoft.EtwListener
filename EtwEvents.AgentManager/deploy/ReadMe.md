## Installing ETW AgentManager as Windows Service

- The installer is typically shipped as a zip archive, with this file in it.

- In addition you will also require:
  
  1) A web server certificate, required on new installations, can be re-used on updates.
     - The certificate can be installed manually (or it could be already installed, e.g. if it is a wildcard certificate)
       - When installed, manually configure its specifics in `appsettings.Local.json` - see below under [Local Configuration](#local-configuration).
     - The server certificate can be provided as a file:
       - it must be copied into this directory (once unzipped).
       - The certificate must be a PKCS12 certificate with a `.p12` file extension.
       - The installer will use the first matching certificate it can find in this directory.
       - In this case the installer will update `appsettings.Local.json` accordingly.
  2) Any root or intermediate CA certificates, typically required to validate client certificates (user or agent).
     - These certificates must have the `.cer` file extension.

- Steps:
  
  1) Unzip archive.
  2) Copy certificates into unzipped folder (if provided).
  3) Modify `appsettings.Local.json` and `authorization.json` if needed, make sure to remove comments as they cause errors.
  4) Double click `InstallService.cmd`, follow prompts.

### User Authentication

One of these applies (when prompted by the browser):

- Use a client certificate where the subject (DN) includes "role=etw-manager" or "role=etw-admin". 
- Use a client certificate where the Common Name (CN) is listed in authorization.json under ClientValidation.

### Agent Authentication

One of these applies:

- Use a client certificate where the subject (DN) includes "role=etw-pushagent".
- Use a client certificate where the Common Name (CN) is listed in authorization.json under AgentValidation.

### Local Configuration

- Optionally, change the settings in `appsettings.Local.json` and/or `authorization.json` before running InstallService.cmd.
  - Note: Powershell 5.1 (default version) does not support comments in JSON files.
  - Note: when running the installer to update an already existing install, then the installed configuration files
    will be merged back into the new configuration files before being replaced, to preserve the existing configuration.

- The settings in `appsettings.Local.json` can selectively override settings in `appsettings.json` without having to duplicate the entire file.
      
- `authorization.json`:
  This example will add John Doe in as an authorized user for the agent manager (common name of certificate must be "John Doe"):
  ```json
  "ClientValidation": {
    "AuthorizedCommonNames": [
      "John Doe"
    ]
  },
  ```

- `authorization.json`:
  This example will revoke the client certificate with thumbprint cd91bf6d1f52b76285b5b96abb57381d8d92bfa5:
  ```json
  RevokedCertificates": [
    "cd91bf6d1f52b76285b5b96abb57381d8d92bfa5"
  ],
  ```

- `appsettings.Local.json`:
  This example will specify which certificate to load as server certificate.
  (only required if certificate is already installed, and no certificate is found in the installer folder, otherwise the installer will update `appsettings.Local.json`).
  ```json
  "Kestrel": {
    "EndpointDefaults": {
      "Protocols": "Http1AndHttp2"
    },
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:80"
      },
      "Https": {
        "Url": "https://0.0.0.0:443"
        "Certificate": { // Windows only, will select the matching certificate with the latest expiry date
          "Subject": "kd-soft.net", // matches any Subject containing that string
          "Store": "My", // My = Personal store, seems like only stores indicated by the StoreName type are accepted
          "Location": "LocalMachine",
          "AllowInvalid": false //"<true or false; defaults to false, must be false for server certificates>"
        }
      }
    }
  },
  ```
