FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-noble

WORKDIR /usr/src/app

# Bundle App and basic config
COPY Credfeto.Nuget.Proxy .
COPY appsettings.json .
COPY healthcheck .

EXPOSE 8080
ENTRYPOINT [ "/usr/src/app/Credfeto.Nuget.Proxy.Server" ]
 
# Perform a healthcheck.  note that ECS ignores this, so this is for local development
HEALTHCHECK --interval=5s --timeout=2s --retries=3 --start-period=5s CMD [ "/usr/src/app/healthcheck" ]
