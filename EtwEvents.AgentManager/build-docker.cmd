pushd ..
docker build --force-rm -f ./EtwEvents.AgentManager/Dockerfile -t kdsoftetwagent --target final .
popd
