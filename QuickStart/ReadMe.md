# QUICK START

1) Prerequisites: 
   
   - Recent LTS versions of [Node.js](https://nodejs.org/en/) and NPM installed.

2) Clone repository.

3) Run `InstallDevCertificates.cmd` in the root/solution directory.

4) Open directory `EtwEvents.AgentManager/wwwroot` and run `npm run build`.

5) Open Visual Studio 2022 in Administrator mode ("Run as Administrator") and load the solution.

6) Rebuild solution.

7) Run EtwEvents.AgentManager with launch profile "EtwEvents.AgentManager Dev".

8) Run EtwEvents.PushAgent with launch profile "EtwEvents.PushAgent Dev".

9) When browser prompts for client certificate, pick the one named "Dev Admin".

10) In the side bar select the "my-dev-site" agent.

11) On the Configuration tab, click the "Import Configuration" button and load the `./QuickStart/my-dev-site.json` file, then click the ""Apply All"" button.

12) Then click the "Start Session" in the sidebar entry for the "my-dev-site" agent, this will log a selection of .NET runtime events to a file.
