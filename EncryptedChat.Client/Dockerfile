# syntax=docker/dockerfile:1

# --- Stage 1: Tailwind v4 CSS (output.css is gitignored, must be built) ---
FROM node:20-alpine AS css
WORKDIR /client
COPY EncryptedChat.Client/package.json EncryptedChat.Client/
RUN cd EncryptedChat.Client && npm install
COPY EncryptedChat.Client/ EncryptedChat.Client/
RUN cd EncryptedChat.Client && npx @tailwindcss/cli -i wwwroot/css/input.css -o wwwroot/css/output.css --minify

# --- Stage 2: publish the Blazor WASM client ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY EncryptedChat.Client/EncryptedChat.Client.csproj EncryptedChat.Client/
RUN dotnet restore EncryptedChat.Client/EncryptedChat.Client.csproj
COPY EncryptedChat.Client/ EncryptedChat.Client/
COPY --from=css /client/EncryptedChat.Client/wwwroot/css/output.css EncryptedChat.Client/wwwroot/css/output.css
RUN dotnet publish EncryptedChat.Client/EncryptedChat.Client.csproj -c Release -o /app/publish --no-restore

# --- Stage 3: optionally obfuscate the published wwwroot JS (OBFUSCATE=true) ---
# .NET<->JS interop calls JS by string name, so --rename-globals and --rename-properties
# stay OFF (defaults) — window.* entry-points (e.g. window.encryptedChatCrypto) and interop
# method names (e.g. encryptedChatCrypto.generateIdentityKeyPair) are preserved.
# --string-array true --string-array-encoding base64: string literals in the SOURCE become
# base64 tokens decoded at runtime — grep for "encryptedChatCrypto" in the obfuscated file
# returns 0 hits, but the name resolves correctly at runtime (window["encryptedChatCrypto"]
# is populated). OBFUSCATE=false => passthrough. We obfuscate from a copied source dir into
# wwwroot/js to avoid in-place input==output ambiguity, then drop the stale precompressed
# .gz/.br so the obfuscated plaintext is what nginx serves.
FROM node:20-alpine AS jsobf
ARG OBFUSCATE=false
WORKDIR /pub
COPY --from=build /app/publish/wwwroot ./wwwroot
RUN if [ "$OBFUSCATE" = "true" ]; then \
        cp -r wwwroot/js /tmp/js-src && \
        npx --yes javascript-obfuscator@5.4.3 /tmp/js-src --output wwwroot/js \
          --compact true --string-array true --string-array-encoding base64 --rename-globals false && \
        rm -rf /tmp/js-src && rm -f wwwroot/js/*.gz wwwroot/js/*.br ; \
    fi

# --- Stage 4: serve static WASM + reverse-proxy ---
FROM nginx:alpine AS runtime
COPY --from=jsobf /pub/wwwroot /usr/share/nginx/html
COPY EncryptedChat.Client/nginx.conf /etc/nginx/conf.d/default.conf
COPY EncryptedChat.Client/docker-entrypoint.d/40-runtime-config.sh /docker-entrypoint.d/40-runtime-config.sh
RUN chmod +x /docker-entrypoint.d/40-runtime-config.sh
EXPOSE 80
