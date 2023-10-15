#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
# need custom root certificate in dev and prod images
WORKDIR /usr/local/share/ca-certificates
COPY ["./EtwEvents.AgentManager/certificates/Kd-Soft.crt", "./"]
RUN update-ca-certificates
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
# we need node/npm for SPA build steps in wwwroot
RUN apt-get update
RUN apt-get install -y ca-certificates curl gnupg
RUN mkdir -p /etc/apt/keyrings
RUN curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg
RUN echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_18.x nodistro main" | tee /etc/apt/sources.list.d/nodesource.list
RUN apt-get update
RUN apt-get install -y nodejs

# projects
WORKDIR /src
COPY ["common.props", "common.props"]
COPY ["EventSinks/eventSink.props", "EventSinks/eventSink.props"]
COPY ["EventSinks/eventSink.targets", "EventSinks/eventSink.targets"]
# shared projects
COPY ["EtwEvents.Shared/EtwEvents.Shared.csproj", "EtwEvents.Shared/"]
COPY ["EtwEvents.Shared.EventSink/EtwEvents.Shared.EventSink.csproj", "EtwEvents.Shared.EventSink/"]
# gRPCSink project
COPY ["EventSinks/EtwEvents.gRPCSink/EtwEvents.gRPCSink.csproj", "EventSinks/EtwEvents.gRPCSink/"]
COPY ["EtwEvents.Protobuf.Shared/EtwEvents.Protobuf.Shared.csproj", "EtwEvents.Protobuf.Shared/"]
RUN dotnet restore "EventSinks/EtwEvents.gRPCSink/EtwEvents.gRPCSink.csproj"
# main AgentManager project
COPY ["EtwEvents.AgentManager/EtwEvents.AgentManager.csproj", "EtwEvents.AgentManager/"]
COPY ["EtwEvents.Client.Shared/EtwEvents.Client.Shared.csproj", "EtwEvents.Client.Shared/"]
RUN dotnet restore "EtwEvents.AgentManager/EtwEvents.AgentManager.csproj"
# copy the rest
COPY . .
# SPA build steps
WORKDIR "/src/EtwEvents.AgentManager/wwwroot"
RUN npm install
RUN npm run prepare-release
# build AgentManager
WORKDIR "/src/EtwEvents.AgentManager"
RUN dotnet build "EtwEvents.AgentManager.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EtwEvents.AgentManager.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
# need custom root certificate only in dev image, for production we install it at run time (run-docker.cmd)
#WORKDIR /usr/local/share/ca-certificates
#COPY ["./EtwEvents.AgentManager/certificates/Kd-Soft.crt", "./"]
#RUN update-ca-certificates
WORKDIR /app
COPY ["./EventSinks/Deploy", "./EventSinks"]
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "KdSoft.EtwEvents.AgentManager.dll"]