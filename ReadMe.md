# ETW LISTENER

A system to extract ETW (Event Tracing for Windows) events and collect them centrally.

## Overview

An EtwListener deployment consists of two types of nodes:

1) One or more agents: they are installed on each machine that needs log collection.

2) One agent manager: it configures, starts and stops the agents.

Agents communicate with the agent manager by establishing an SSE (Server Sent Events) connection to the configured agent manager. The agent manager in turn uses this connection to "push" control messages to the agents.

* Some control messages update the agent's configuration or start/stop the agent.

* Some control messages prompt the agent to report status updates.

* All authentication between Agent and Agent Manager, as well as between User and Agent Manager is based on the use of client certificates.

A typical deployment example with Elastic Search as log destination:

![ETW Events.png](./doc/ETW%20Events.png)

## Agent

An agent is a Windows service that can make outging network connections.

* It is always connected to the agent manager configured during the install.

* Depending on configuration, the agent can also connect to an external endpoint to send the log events to.

* For installation instructions, see [Install.md](./EtwEvents.PushAgent/Install.md) and [deploy/ReadMe.md](./EtwEvents.PushAgent/deploy/ReadMe.md).

## Agent Manager

The agent manager is a web site that allows for configuring, starting and stopping connected agents.

* The agent manager does not connect to the agents, it is the agents' responsibility to connect to the agent manager.
* For build and run instructions, see [ReadMe.md](./EtwEvents.AgentManager/ReadMe.md).
* Installation can be done using Docker, or as described in [Install.md](./EtwEvents.AgentManager/Install.md) and [deploy/ReadMe.md](./EtwEvents.AgentManager/deploy/ReadMe.md).

### UI Overview

![AgentManager.png](./doc/AgentManager.png)
