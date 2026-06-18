#!/usr/bin/env bash
# Publish the Blazor WASM client (Release), then obfuscate its wwwroot JS.
# NOTE: the .NET app assembly ships as Webcil (EncryptedChat.Client.wasm, a binary
# blob — not source) and is integrity-hashed in blazor.boot.json, so it is NOT
# obfuscated here. We obfuscate the human-readable wwwroot JS instead (interop
# globals/property names preserved — see obfuscate-client-js.sh).
set -euo pipefail
cd "$(dirname "$0")/.."

rm -rf publish/client
# Explicit .csproj for consistency with the API script.
dotnet publish EncryptedChat.Client/EncryptedChat.Client.csproj -c Release -o publish/client --nologo

./scripts/obfuscate-client-js.sh

echo "Client publish (JS obfuscated) ready in publish/client/wwwroot"
