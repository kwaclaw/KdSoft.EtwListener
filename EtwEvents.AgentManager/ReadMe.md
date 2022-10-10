# Build And Run Instructions

## Import Styles before production build/publish or debugging in development environment

Any CSS that is not available as CSS-in-JS in the form of lit-element css`...my styles...` must
be imported/converted to that format. Place your CSS into `wwwroot/css` and then run `npm run prepare` in `wwwroot`.

Or better, just run `npm run build` in `wwwroot`, which performs an NPM install followed by all other processing.

## Debug with Visual Studio

- To allow external clients to connect to the agent manager, you must use public domain name a matching server certificate.
- When switching between Docker and regular debugging, rebuild the project.

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
