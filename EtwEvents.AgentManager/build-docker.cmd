pushd ..
docker build --force-rm -f ./EtwEvents.AgentManager/Dockerfile -t kdsoft-etw-agent-manager --target final .
popd
