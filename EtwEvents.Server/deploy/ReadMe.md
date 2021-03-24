## Prepare Install Package

- Open project EtwEvents.Server and publish it using either the Platform or the SelfContained profile.
- If using the Platform profile, make sure the target system has the target framework (specified in the profile) installed.

## Install Package

- After preparing the install package, copy the project folder "deploy" to a (temporary) location on the target system.
- If applicable, check that the publish profile's target framework is installed.
- On a new installation
    - Edit the included "appsettings.Local.json" according to the local requirements (see [Local Configuration](#local-configuration) below).
- On an existing installation:
    - Take note of the current install directory, if it needs to stay the same
    - Update the current "appsettings.Local.json" file if changes are desired.
- Check that the target system's firewall has the required inbound port open (for TCP, default is 50052 unless overridden locally).
- Check that the root certificate for client validation is installed, its thumbprint must match the value configured in "appsettings.Local.json".
- Check that the server certificate for the application is installed, as configured in the "Endpoints" section of "appsettings.Local.json".
- Finally, run "CreateService.cmd \<target directory>" as administrator:
    - \<target directory> is optional, it defaults to "C:\EtwEvents.Server", on an existing installation
      it may optionally match current the install directory from above.
    - This will install the application as a windows service called "Etw Listener".

### Local Configuration

- Override the settings in appsettings.json by editing the file appsettings.Local.json in the deploy folder.
- The settings in "appsettings.Local.json" can selectively override settings in "appsettings.json" without having to duplicate the entire file.
- This example will only ovveride the Directory in the RollingFile section:
```json
  "Logging": {
    "RollingFile": {
      "Directory": "../Logs",
    }
  },
```

### TLS, Connection Security

Connection security and authentication/authorization are both performed using certificates.
Specified in the Kestrel section.

```json
  "Kestrel": {
    "EndpointDefaults": {
      "Protocols": "Http2"
    },
    "Endpoints": {
      "Https": {
        "Url": "https://*:50052",
        WE CAN SPECIFY A CERTIFICATE FILE
        "Certificate": {
          "Path": "C:\\EtwEvents.Server\\example.p12",
          "Password": "XXXXXXXXXXXXX"
        }
        OR WE CAN USE THE LOCAL CERTIFICATE STORAGE
        // will select the matching certificate with the latest expiry date
        "Certificate": {
          "Subject": "example.com", // matches any Subject containing that string
          "Store": "My" / Only stores indicated by the StoreName type are accepted
          "Location": "LocalMachine",
          "AllowInvalid": false // must be false for server certificates>
        }
      }
    }
  }
```

### Client Authentication

We can use a self-signed root certificate , as client authentication certificates are provided by us. 

- The self-signed root certificate must be installed in the "**Local Computer\Trusted Root Certification Authorities**" folder of the local certificate storage.
- The client certificate must be configured to support client authorization.

A useful tool for creating certificates is [XCA](https://www.hohnstaedt.de/xca/).

We specify authorized clients in appsettings.Local.json, in the **ClientValidation** section:
```json
  "ClientValidation": {
    "RootCertificateThumbprint": "d87dce532fb17cabd3804e77d7f344ec4e49c80f",
    "AuthorizedCommonNames": [
      "John Doe"
    ]
  }
```

