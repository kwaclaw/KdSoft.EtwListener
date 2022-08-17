## Installing ETW AgentManager as Windows Service

- The installer is typically shipped as a zip archive, with this file in it.

- In addition you will also require a server certificate (required on new installations, can be re-used on updates):
  
  - The server certificate must be copied into this directory (once unzipped).
  - The certificate must be a PKCS12 certificate with a `.p12` file extension.
  - It must be a proper web server certificate.
  - The installer will use the first matching certificate it can find in this directory.

- Steps:
  
  1) Unzip archive.
  2) copy certificate into unzipped folder (if provided).
  3) Double click `InstallService.cmd`, follow prompts.

### User Authentication

One of these applies (when prompted by the browser):

- Use a client certificate where the subject (DN) includes "role=etw-manager" 
- Use a client certificate where the Common Name (CN) is listed in appsettings.json under ClientValidation

### Agent Authentication

One of these applies:

- Use a client certificate where the subject (DN) includes "role=etw-pushagent" 
- Use a client certificate where the Common Name (CN) is listed in appsettings.json under AgentValidation

### Local Configuration

- Optionally, change the settings in `appsettings.Local.json`.
  
  - Note: this will override/replace settings in appsettings.json, but it is not possible to remove existing settings.

- This example will add John Doe as an authorized user for the agent manager (common name of certificate must be "John Doe"):
  
  ```json
  "ClientValidation": {
    // optional, only set when a specific root certificate must be used for client authorization
    "RootCertificateThumbprint": "d87dce532fb17cabd3804e77d7f344ec4e49c80f",
    "AuthorizedCommonNames": [
      "John Doe"
    ]
  },
  ```
