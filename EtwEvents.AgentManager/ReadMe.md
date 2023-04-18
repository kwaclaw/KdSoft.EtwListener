# Build And Run Instructions

## Import Styles before production build/publish or debugging in development environment

Any CSS that is not available as CSS-in-JS in the form of lit-element css`...my styles...` must
be imported/converted to that format. Place your CSS into `wwwroot/css` and then run `npm run build` in `wwwroot`.

Or better, just run `npm install` in `wwwroot`, which performs an NPM install followed by `npm run build`.

## Import Development Certificates

- Run `InstallDevCertificates.cmd` in the solution directory.
- When the browser prompts for a client certificate, pick the one named "Dev Admin".
- Note: The server certificate does not need to be installed, as it is loaded from the certificates directory - see appsettings.Development.json.

## Debug with Visual Studio 2022

- For the PushAgent project Visual Studio must be running in Administrator mode.
- Debug with the `EtwEvents.AgentManager Dev` launch profile to be able to use the development server certificate for localhost.
  - If debugging the PushAgent simultaneously, make sure the PushAgent uses the `EtwEvents.PushAgent Dev` profile.
- To allow external clients to connect to the agent manager, you must use a publicly visible domain name with a matching server certificate.
  - Modify the file appsettings.Personal.json in EtwEvents.AgentManager to load the right certificate.
  - Modify the profile "EtwEvents.AgentManager Personal" in EtwEvents.AgentManager to use the new Url.
  - Modify the file appsettings.Personal.json in EtwEvents.PushAgent to connect to the new Url
  - When debugging make sure the AgentManager uses the launch profile "EtwEvents.AgentManager Personal".
  - When debugging make sure the PushAgent uses the launch profile "EtwEvents.PushAgent Personal".
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
  
  ### Localization
  
  Localization is based on the gettext system. See [Wikipedia](https://en.wikipedia.org/wiki/Gettext).
  It is currently incomplete.
  
  - On the Javascript side we use [gettext.js](https://github.com/guillaumepotier/gettext.js).
    - Make a phrase localizable by using `i18n.__('My Phrase')`, where `i18n` is a global object.
    - Then add localizations to the `.po` files under `EtwEvents.AgentManager\wwwroot`.
  - For editing `.po` files we can use the free tool [POEdit]( https://poedit.net/).
  - In C# we use [OrchardCore.Localization.Core](https://github.com/OrchardCMS/OrchardCore/tree/main/src/OrchardCore/OrchardCore.Localization.Core).
    - Make a phrase localizable by using `_.GetString("My Phrase")`, where `_` represents the injected `IStringLocalizer`.
    - Then add localizations to the `.po` files under `EtwEvents.AgentManager\Resources`.
