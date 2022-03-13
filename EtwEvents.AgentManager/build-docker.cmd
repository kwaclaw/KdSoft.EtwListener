pushd ..
docker build --force-rm -f ./EtwEvents.AgentManager/Dockerfile -t waclawek/kdsoft-etw-agent-manager:1.0 --target final .
popd
pause
