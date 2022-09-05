# Build And Run Instructions (mostly for Docker)

## Import Styles before production build/publish or development run

Any CSS that is not available as CSS-in-JS in the form of lit-element css`...my styles...` must
be imported/converted to that format. Place your CSS into `wwwroot/css` and then run `npm run prepare` in `wwwroot`.

Or better, just run `npm run build` in `wwwroot`, which performs an NPM install followed by all other processing.

## Debug with Visual Studio in Docker

- Before debugging, switch to wwwroot directory and run `npm install`, if necessary
- Then debug with the Docker launch settings selected.

## Build Docker image for production

- Before build: build all EventSinks projects so that `EventSinks/Deploy/` gets populated.
- Switch to `AgentManager` project directory
- Run `build-docker.cmd`

## Run Docker image for production

Use the `docker run` command with arguments as described below. As an example review `run-docker.cmd` in the `EtwEvents.AgentManager` directory.

#### Asp.NET 6.0 specific arguments

We neeed to mount some directories required by Asp.NET 6.0 into pre-determined paths in the container:

- ONLY IN DEVELOPMENT ENVIRONMENT (Debugging): Mount user secrets directory to `/root/.microsoft/usersecrets`
  e.g. `--mount type=bind,src="%APPDATA%\\Microsoft\\UserSecrets\\",dst=/root/.microsoft/usersecrets/,readonly`
  OTHERWISE use environment variables to pass secrets
  e.g. `-e "Kestrel:Endpoints:Https:Certificate:Password=?????????"`

- Mount Https directory to "/root/.aspnet/https"
  e.g. `--mount type=bind,src="%APPDATA%\\ASP.NET\\Https\\",dst=/root/.aspnet/https/,readonly`

- Mount a data protection directory to `/root/.aspnet/DataProtection-Keys`
  e.g. `--mount type=bind,src="C:\\Temp\\DP-Keys\\",dst=/root/.aspnet/DataProtection-Keys/`

#### Server Certificate arguments

- Obtain  a server certificate, or generate your own (based on a custom CA certificate). It must be in PKCS12 format.

- Place it in a directory and mount that directory into the docker container:
   e.g. `--mount type=bind,src=c:/apps/certificates//,dst=/app/certs/,readonly`

- Override the certificate path in `appsettings.json`
  e.g. `-e "Kestrel:Endpoints:Https:Certificate:Path=/app/certs/server-cert.p12"`

- Override the certificate password in `appsettings.json` (or pass it to "docker run")
  e.g. `-e "Kestrel:Endpoints:Https:Certificate:Password=?????????"`

#### If Needed - Custom Root Certificate

- You server certificate may depend on a custom root certificate, or intermediate CA certificate.
- Once the container (e.g. MyContainer) is up and running, copy and install the root certificate, like in this example:
  `docker cp "C:/MyCerts/Kd-Soft.crt" MyContainer:"/usr/local/share/ca-certificates"`
  `docker exec -dt -w "/usr/local/share/ca-certificates" MyContainer update-ca-certificates`
- See also run-docker.cmd

#### Event Sink arguments

- Event Sinks are stored in the `/app/EventSinks` directory in the container.

- This directory can also be mounted externally, which will replace the existing directory in the container:
  e.g. `--mount type=bind,src=c:/app/EventSinks,dst=/app/EventSinks/`

- **Note:** If there are two versions of the same event sink type, they still must be copied to two different directories.

#### Docker Networking

Detailed instructions are out of scope, but there is plenty of documentation online.

**Note about Docker Desktop**:

- When you are using Docker Desktop, then by default the firewall blocks incoming requests, look for rules named "Docker Desktop Backend".
- Disable 'Block' rules, enable 'Allow' rules, also for Public networks.
- It is recommended to use a user-defined bridge network instead of the default:
  - pass `--network my-net` argument to docker run,
  - or call `docker network connect my-net my-container`

### Client Authentication

Both, the user accessing the agent manager, and the ETW agent accessing the agent manager are considered clients that need to be authenticated.
We use client certificates for both.

- The client certificate must be configured to support client authorization.
- The client certificate presented by the PushAgent will be authorized if the DN contains role=etw-pushagent.
- The client certificate presented by the user/browser will be authorized if the DN contains role=etw-manager.
- If a client certificate does not have the role above, it can be authorized by being listed in the AuthorizedCommonNames setting (see below).

- If needed, custom CA certificates must be installed.
  - On a Windows client:
    - the custom root certificate must be installed in "**Local Computer\Trusted Root Certification Authorities**".
    - a custom intermediate CA certificate must be installed in "**Local Computer\Intermediate Certification Authorities**".
  - On a Linux client it depends on the distribution. A popular way is:
    - copy `Kd-Soft.crt` to `/usr/local/share/ca-certificates/`
    - run `update-ca-certificates` with the proper permissions (root)
  - We can restrict validation to those client certificates that are derived from the custom root certificate - see below.

- A useful GUI tool for creating certificates is [XCA](https://www.hohnstaedt.de/xca/).
- We also have OpenSSL scripts, they require OpenSSL 3.0 installed:
  - see https://slproweb.com/products/Win32OpenSSL.html or https://kb.firedaemon.com/support/solutions/articles/4000121705.
  - for server certificates: located in the `EtwEvents.AgentManager/certificates` directory.
  - for client certificates: located in the `EtwEvents.PushAgent/certificates` directory.

- We specify authorized users/agents in `authorization.json`, in the **ClientValidation** and **AgentValidation** section.
  "Unauthorization" can be done by removing an authorized common name, or by revoking the associated certificate, e.g.:
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
