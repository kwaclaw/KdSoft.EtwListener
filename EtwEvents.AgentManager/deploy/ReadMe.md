## Installing ETW AgentManager as Windows Service

- The installer is shipped as a zip archive, with this file in it.

- In addition you will also require a server certificate (required on new installations, can be re-used on updates):
     - The server certificate must be copied into this directory (once unzipped).
     - The certificate must be a PKCS12 certificate with a `.p12` file extension.
     - It must be a proper web server certificate.
     - The installer will use the first matching certificate it can find in this directory.

- Steps:
  
  1) Unzip archive.
  2) copy certificate into unzipped folder (if provided).
  3) Double click `InstallService.cmd`, follow prompts.

### Local Configuration

- Optionally, change the settings in `appsettings.json`.

- This example will add Brandon Lewis as an authorized to use the agent manager:
  
  ```json
  "ClientValidation": {
    "RootCertificateThumbprint": "d87dce532fb17cabd3804e77d7f344ec4e49c80f",
    "AuthorizedCommonNames": [
      "Karl Waclawek",
      "Tom Chandler",
      "Brandon Lewis"
    ]
  },
  ```
