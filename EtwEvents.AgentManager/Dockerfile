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
COPY ["EtwEvents.Protobuf.Shared/EtwEvents.Protobuf.Shared.csproj", "EtwEvents.Protobuf.Shared/"]
# ElasticSink project
COPY ["EventSinks/EtwEvents.ElasticSink/EtwEvents.ElasticSink.csproj", "EventSinks/EtwEvents.ElasticSink/"]
RUN dotnet restore "EventSinks/EtwEvents.ElasticSink/EtwEvents.ElasticSink.csproj"
# MongoSink project
COPY ["EventSinks/EtwEvents.MongoSink/EtwEvents.MongoSink.csproj", "EventSinks/EtwEvents.MongoSink/"]
RUN dotnet restore "EventSinks/EtwEvents.MongoSink/EtwEvents.MongoSink.csproj"
# RollingFileSink project
COPY ["EventSinks/EtwEvents.RollingFileSink/EtwEvents.RollingFileSink.csproj", "EventSinks/EtwEvents.RollingFileSink/"]
RUN dotnet restore "EventSinks/EtwEvents.RollingFileSink/EtwEvents.RollingFileSink.csproj"
# SeqSink project
COPY ["EventSinks/EtwEvents.SeqSink/EtwEvents.SeqSink.csproj", "EventSinks/EtwEvents.SeqSink/"]
RUN dotnet restore "EventSinks/EtwEvents.SeqSink/EtwEvents.SeqSink.csproj"
# main AgentManager project
COPY ["EtwEvents.AgentManager/EtwEvents.AgentManager.csproj", "EtwEvents.AgentManager/"]
COPY ["EtwEvents.Client.Shared/EtwEvents.Client.Shared.csproj", "EtwEvents.Client.Shared/"]
RUN dotnet restore "EtwEvents.AgentManager/EtwEvents.AgentManager.csproj"
# copy the rest
COPY . .
# build event sinks
WORKDIR "/src/EventSinks/EtwEvents.ElasticSink"
RUN dotnet build "EtwEvents.ElasticSink.csproj" --no-incremental
WORKDIR "/src/EventSinks/EtwEvents.MongoSink"
RUN dotnet build "EtwEvents.MongoSink.csproj" --no-incremental
WORKDIR "/src/EventSinks/EtwEvents.RollingFileSink"
RUN dotnet build "EtwEvents.RollingFileSink.csproj" --no-incremental
WORKDIR "/src/EventSinks/EtwEvents.SeqSink"
RUN dotnet build "EtwEvents.SeqSink.csproj" --no-incremental
# SPA build steps
WORKDIR "/src/EtwEvents.AgentManager/wwwroot"
RUN npm install && npm run build
# build AgentManager
WORKDIR "/src/EtwEvents.AgentManager"
RUN dotnet build "EtwEvents.AgentManager.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EtwEvents.AgentManager.csproj" -c Release -o /app/publish

FROM base AS final
# need custom root certificate in dev and prod images
WORKDIR /usr/local/share/ca-certificates
COPY ["./EtwEvents.AgentManager/certificates/Kd-Soft.crt", "./"]
RUN update-ca-certificates
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=build /src/EventSinks/Deploy ./EventSinks
ENTRYPOINT ["dotnet", "KdSoft.EtwEvents.AgentManager.dll"]