pushd ..
docker build --force-rm -f ./EtwEvents.AgentManager/Dockerfile -t kdsoft-etw-agent-manager:1.0 --target final .
popd
pause
