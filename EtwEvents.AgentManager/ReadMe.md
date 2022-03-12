# Build

## Import Styles before production build or development run

Any CSS that is not available as CSS-in-JS in the form of lit-element css`...my styles...` must
be imported/converted to that format. Place your CSS into Spa/css and then run "npm run prepare" in wwwroot.

## Debug in Docker

- Before debugging, switch to wwwroot directory and run "npm install", if necessary
- Then debug with the Docker launch settings selected.
    
## Build  and Run Docker image for production

- To build: run the build-docker.cmd script
- To run: run the run-docker.cmd script

## Certificates

Connection security and authentication/authorization are both performed using certificates.
- The server certificate must be placed in directory mounted into the Docker image (see run-docker.cmd).
- The appSettings entry for the certificate path must be added as environment variable in run-docker.cmd.
- The appSettings entry for the certificate password can be handled as a user secret, or it can be added as an 
environment variable in run-docker.cmd.


### TLS

Specified in appsettings.json in the Kestrel section.

```json
  "Kestrel": {
    "EndpointDefaults": {
      "Protocols": "Http1AndHttp2"
    },
    "Endpoints": {
      "Https": {
        "Url": "https://0.0.0.0:5099",
        WE CAN SPECIFY A CERTIFICATE FILE (must be used for Docker)
        "Certificate": {
          "Path": "D:\\PlayPen\\EtwListener\\localhost.p12",
          "Password": "XXXXXXXXXXXXX"
        }
        OR WE CAN USE THE LOCAL CERTIFICATE STORAGE (Windows only)
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

### Client/User Authentication

We can use a self-signed root certificate , as client authentication certificates are provided by us. 

- The self-signed root certificate must be installed in the "**Local Computer\Personal**" folder of the local certificate storage.
- The client certificate must be configured to support client authorization.

A useful tool for creating certificates is [XCA](https://www.hohnstaedt.de/xca/).

We specify authorized users in appsettings.json, in the **ClientValidation** section:

```json
  "ClientValidation": {
    "RootCertificateThumbprint": "d87dce532fb17cabd3804e77d7f344ec4e49c80f",
    "AuthorizedCommonNames": [
      "karl@waclawek.net"
    ]
  }
```

### Authentication as Client of Grpc Server

We must have obtained a certificate from the site owning the server. This certificate must be installed in the  "**CurrentUser\Personal**" folder of the local certificate storage.

The common name in the certificate must correspond to one of the authorized common names configured on the Grpc server.

We specify the certificate we use to authorize this web application as the client of the Grpc server in appsettings.json as well, in the **ClientCertificate** section:

```json
  "ClientCertificate": {
    "Location": "CurrentUser",
    "Thumbprint": "558efcc579f8b7be36f02d709cf58ea29020644e"
  }
```

# Event Sinks

## Location of Binaries

Each event sink implementation must be located in a unique folder under the EventSinks directory.

**Note:** If there are two versions of the same event sink type, they still must be copied to two different directories.

## Location of web interface components

- These components provide a configuration UI for a specific event sink and version.

- They must be located under the Spa/eventSinks directory and reflect the same directory structure as the binaries so they can be mapped.

- The file names must match the patterns `*-config.js` and `*-config-model.js`.

**Note:** If web components are used they must have unique names, even between event sinks of the same type.



