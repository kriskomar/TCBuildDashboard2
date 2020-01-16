FROM microsoft/dotnet:2.1-aspnetcore-runtime AS base
WORKDIR /app

FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /src
COPY ["TeamCityBuildMonitor/TeamCityBuildMonitor.csproj", "TeamCityBuildMonitor/"]
RUN dotnet restore "TeamCityBuildMonitor/TeamCityBuildMonitor.csproj"
COPY . .
WORKDIR "/src/TeamCityBuildMonitor"
RUN dotnet build "TeamCityBuildMonitor.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "TeamCityBuildMonitor.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
VOLUME /logs
ENTRYPOINT ["dotnet", "TeamCityBuildMonitor.dll"]