# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble-aot AS build
WORKDIR /source

# Restore dependencies separately so source-only changes can reuse this layer.
COPY Directory.Build.props Directory.Packages.props ./
COPY build/ ./build/
COPY src/SlingNetwork/SlingNetwork.csproj ./src/SlingNetwork/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/SlingNetwork/SlingNetwork.csproj --runtime linux-x64

COPY src/SlingNetwork/ ./src/SlingNetwork/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/SlingNetwork/SlingNetwork.csproj \
        --configuration Release \
        --runtime linux-x64 \
        --self-contained true \
        --no-restore \
        --output /app/publish \
        -p:InvariantGlobalization=true \
    && rm -f /app/publish/sling.dbg

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled AS final
WORKDIR /app

LABEL org.opencontainers.image.source="https://github.com/dotnet-campus/SlingNetwork" \
      org.opencontainers.image.description="SlingNetwork P2P punching and networking service"

COPY --link --from=build /app/publish/ ./

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

# These ports document the default bridge-network setup. The production Compose
# deployment uses host networking and a configurable UDP range instead.
EXPOSE 5451/tcp 50000/udp

USER $APP_UID
ENTRYPOINT ["./sling"]
CMD ["serve", "--listen", "0.0.0.0:5451", "--udp-port-range", "50000"]
