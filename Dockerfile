FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG VERSION=0.0.0
WORKDIR /src
# No csproj-only restore layer: restored without the .razor files, the SDK leaves out the
# framework assets and _framework/blazor.web.js is missing from the publish (404).
COPY src/Factos/ src/Factos/
RUN dotnet publish src/Factos/Factos.csproj -c Release -o /app -p:Version=$VERSION

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
# Owned by app: Paramètres › Mises à jour overwrites these files (until the container is recreated).
COPY --from=build --chown=app:app /app .
# Everything that must survive the container lives in App_Data (the volume): the SQLite
# database (wizard default App_Data/app.db), Data Protection keys, logos, and the
# appsettings.local.json the /setup wizard writes (Setup:LocalConfigFile).
RUN mkdir App_Data && chown app:app App_Data
ENV Setup__LocalConfigFile=App_Data/appsettings.local.json \
    DataProtection__KeysPath=App_Data/keys \
    Storage__UploadsPath=App_Data/uploads
VOLUME /app/App_Data
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Factos.dll"]
