# syntax=docker/dockerfile:1

# --- Stage 1: Tailwind v4 CSS (output.css is gitignored, must be built) ---
FROM node:20-alpine AS css
WORKDIR /client
COPY EncryptedChat.Client/package.json EncryptedChat.Client/
RUN cd EncryptedChat.Client && npm install
COPY EncryptedChat.Client/ EncryptedChat.Client/
# Tailwind v4 auto-detects content (scans .razor/.html) from the project tree.
RUN cd EncryptedChat.Client && npx @tailwindcss/cli -i wwwroot/css/input.css -o wwwroot/css/output.css --minify

# --- Stage 2: publish the Blazor WASM client ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY EncryptedChat.Client/EncryptedChat.Client.csproj EncryptedChat.Client/
RUN dotnet restore EncryptedChat.Client/EncryptedChat.Client.csproj
COPY EncryptedChat.Client/ EncryptedChat.Client/
# Overlay the freshly built CSS so `dotnet publish` ships it.
COPY --from=css /client/EncryptedChat.Client/wwwroot/css/output.css EncryptedChat.Client/wwwroot/css/output.css
RUN dotnet publish EncryptedChat.Client/EncryptedChat.Client.csproj -c Release -o /app/publish --no-restore

# --- Stage 3: serve static WASM + reverse-proxy ---
FROM nginx:alpine AS runtime
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY EncryptedChat.Client/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
