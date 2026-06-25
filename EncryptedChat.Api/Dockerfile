# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG OBFUSCATE=false
WORKDIR /src
COPY EncryptedChat.Api/EncryptedChat.csproj EncryptedChat.Api/
RUN dotnet restore EncryptedChat.Api/EncryptedChat.csproj
COPY EncryptedChat.Api/ EncryptedChat.Api/
# Obfuscation tooling (used only when OBFUSCATE=true): the dotnet-tools manifest
# (obfuscar.console) + the Obfuscar config. obfuscar.api.xml uses relative paths
# (publish/api -> publish/api-obf), so we publish into publish/api under WORKDIR.
COPY .config/ .config/
COPY obfuscar.api.xml ./
RUN dotnet publish EncryptedChat.Api/EncryptedChat.csproj -c Release -o publish/api --no-restore
RUN if [ "$OBFUSCATE" = "true" ]; then \
        dotnet tool restore && \
        ASPNET_FX=$(find /usr/share/dotnet/shared/Microsoft.AspNetCore.App -maxdepth 1 -mindepth 1 -type d | sort -V | tail -1) && \
        { [ -n "$ASPNET_FX" ] || { echo "ERROR: ASP.NET Core shared framework not found under /usr/share/dotnet/shared/Microsoft.AspNetCore.App"; exit 1; } ; } && \
        sed "s|<Var name=\"InPath\"|<Var name=\"ExtraFrameworkFolders\" value=\"$ASPNET_FX\" /><Var name=\"InPath\"|" obfuscar.api.xml > obfuscar.api.patched.xml && \
        dotnet obfuscar.console obfuscar.api.patched.xml && \
        cp publish/api-obf/EncryptedChat.dll publish/api/EncryptedChat.dll ; \
    else \
        mkdir -p publish/api-obf && : > publish/api-obf/Mapping.txt ; \
    fi

# Symbol map to de-obfuscate stack traces. NOT a dependency of `runtime`, so a normal
# build never produces it; extract on demand:
#   docker build --target api-map --output type=local,dest=./build-artifacts/api \
#     --build-arg OBFUSCATE=true -f EncryptedChat.Api/Dockerfile .
FROM scratch AS api-map
COPY --from=build /src/publish/api-obf/Mapping.txt /Mapping.txt

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /src/publish/api .
# Attachments are written under /app/storage; create + own the dir as the
# non-root `app` user BEFORE switching to it, so a fresh named volume mounted
# here inherits `app` ownership on first run. (Docker only propagates ownership
# for an empty/new volume; a pre-existing root-owned volume must be recreated.)
RUN mkdir -p /app/storage/attachments && chown -R app:app /app/storage
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# Drop root: run as the image's built-in non-root `app` user.
USER app
ENTRYPOINT ["dotnet", "EncryptedChat.dll"]
