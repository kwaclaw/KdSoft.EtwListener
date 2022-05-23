#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
# need custom root certificate in dev and prod images
WORKDIR /usr/local/share/ca-certificates
COPY ["./EtwEvents.AgentManager/certificates/Kd-Soft.crt", "./"]
RUN update-ca-certificates
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
# we need node/npm for SPA build steps in wwwroot
RUN apt-get update
RUN apt-get install -y curl
RUN apt-get install -y libpng-dev libjpeg-dev curl libxi6 build-essential libgl1-mesa-glx
RUN curl -sL https://deb.nodesource.com/setup_lts.x | bash -
RUN apt-get install -y nodejs
# projects
WORKDIR /src
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
RUN npm run build
# build AgentManager
WORKDIR "/src/EtwEvents.AgentManager"
RUN dotnet build "EtwEvents.AgentManager.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EtwEvents.AgentManager.csproj" -c Release -o /app/publish

FROM base AS final
# need custom root certificate only in dev image, for production we install it at run time (run-docker.cmd)
#WORKDIR /usr/local/share/ca-certificates
#COPY ["./EtwEvents.AgentManager/certificates/Kd-Soft.crt", "./"]
#RUN update-ca-certificates
WORKDIR /app
COPY ["./EventSinks/Deploy", "./EventSinks"]
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "KdSoft.EtwEvents.AgentManager.dll"]