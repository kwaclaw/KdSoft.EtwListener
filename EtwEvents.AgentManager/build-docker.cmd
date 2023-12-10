pushd ..
docker build --force-rm -f ./EtwEvents.AgentManager/Dockerfile -t waclawek/kdsoft-etw-agent-manager:1.2.2 --target final .
popd
pause
