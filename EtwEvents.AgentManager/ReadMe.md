# Build And Run Instructions

## Import Styles before production build or development run

Any CSS that is not available as CSS-in-JS in the form of lit-element css`...my styles...` must
be imported/converted to that format. Place your CSS into `wwwroot/css` and then run `npm run prepare` in `wwwroot`.

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

- Obtain  a server certificate, or generate your own (based on the **Kd-Soft.cer** CA certificate). It must be in PKCS12 format.

- Place it in a directory and mount that directory into the docker container:
   e.g. `--mount type=bind,src=c:/apps/certificates//,dst=/app/certs/,readonly`

- Override the certificate path in `appsettings.json`
  e.g. `-e "Kestrel:Endpoints:Https:Certificate:Path=/app/certs/server-cert.p12"`

- Override the certificate password in `appsettings.json` (or save it as user secret)
  e.g. `-e "Kestrel:Endpoints:Https:Certificate:Password=?????????"`

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

Both, the user accessing the agent manager, and the ETW agent accessing the agent manager are considered clients that need to be authenticated. We use client certificates for both.

We must use a the same self-signed root certificate mentioned above (**Kd-Soft.cer**), as client authentication certificates are provided by us. 

- The client certificate must be configured to support client authorization.

- On a Windows client, the self-signed root certificate must be installed in the "**Local Computer\Personal**" folder of the local certificate storage.

- On a Linux client it depends on the distribution. A popular way is:
  
  - copy `Kd-Soft.cer` to `/usr/local/share/ca-certificates/`
  - run `update-ca-certificates` with the proper permissions (root)

- A useful tool for creating certificates is [XCA](https://www.hohnstaedt.de/xca/).

- We also have OpenSSL scripts in the `EtwEvents.PushAgent/certificates` directory

- We specify authorized users/agents in `appsettings.json`, in the **ClientValidation** or **AgentValidation** section, e.g.:
  
  ```json
  "ClientValidation": {
    "RootCertificateThumbprint": "d87dce532fb17cabd3804e77d7f344ec4e49c80f",
    "AuthorizedCommonNames": [
      "karl@waclawek.net"
    ]
  }
  ```
