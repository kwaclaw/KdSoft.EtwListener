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
- The client certificate presented by the PushAgent will be authenticated if the DN contains role=etw-pushagent.
- The client certificate presented by the AgentManager user will be authenticated if the DN contains role=etw-manager (for the PushAgent client) or rol=etw-manager (for the AgentManager user).
- If a client certificate does not have the role above, it can be authenticated by being listed in the AuthorizedCommonNames setting (see below).

- If needed, a custom root certificate must be installed.
  - On a Windows client, the optional root certificate must be installed in the "**Local Computer\Trusted Root Certification Authorities**" folder of the local certificate storage.
  - On a Linux client it depends on the distribution. A popular way is:
    - copy `Kd-Soft.crt` to `/usr/local/share/ca-certificates/`
    - run `update-ca-certificates` with the proper permissions (root)

- A useful GUI tool for creating certificates is [XCA](https://www.hohnstaedt.de/xca/).
- We also have OpenSSL scripts in the `EtwEvents.AgentManager/certificates` directory, they require OpenSSL 3.0 installed.
  - see https://slproweb.com/products/Win32OpenSSL.html or https://kb.firedaemon.com/support/solutions/articles/4000121705.

- We specify authorized users/agents in `appsettings.json`, in the **ClientValidation** and **AgentValidation** section, e.g.:
  
  ```json
  "ClientValidation": {
    "RootCertificateThumbprint": "d87dce532fb17cabd3804e77d7f344ec4e49c80f",
    // this is only checked when the agent's certificate does not have role=etw-manager
    "AuthorizedCommonNames": [
      "Karl Waclawek",
      "John Doe"
    ]
  },
  "AgentValidation": {
    "RootCertificateThumbprint": "d87dce532fb17cabd3804e77d7f344ec4e49c80f",
    // this is only checked when the agent's certificate does not have role=etw-pushagent
    "AuthorizedCommonNames": []
  }
  ```
