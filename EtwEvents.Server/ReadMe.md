## Certificates

Connection security and authentication/authorization are both performed using certificates.

### TLS

Specified in appsettings.json in the Kestrel section.

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
          "Path": "D:\\PlayPen\\EtwListener\\localhost.p12",
          "Password": "XXXXXXXXXXXXX"
        }
        OR WE CAN USE THE LOCAL CERTIFICATE STORAGE
        // will select the matching certificate with the latest expiry date
        "Certificate": {
          "Subject": "localhost", // matches any Subject containing that string
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

- The self-signed root certificate must be installed in the "**Local Computer\Personal**" folder of the local certificate storage.
- The client certificate must be configured to support client authorization.

A useful tool for creating certificates is [XCA](https://www.hohnstaedt.de/xca/).

We specify authorized clients in appsettings.json, in the **ClientValidation** section:

```json
  "ClientValidation": {
    "RootCertificateThumbprint": "d87dce532fb17cabd3804e77d7f344ec4e49c80f",
    "AuthorizedCommonNames": [
      "karl@waclawek.net"
    ]
  }
```

