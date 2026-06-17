#!/usr/bin/env bash
# Publish the API in Release, then obfuscate EncryptedChat.dll with Obfuscar.
# Dev/test/`dotnet run` are untouched — obfuscation only happens here, on the
# published output. Symbol map lands in publish/api-obf/Mapping.txt (keep it
# to de-obfuscate stack traces; it's gitignored).
set -euo pipefail
cd "$(dirname "$0")/.."

rm -rf publish/api publish/api-obf
# Explicit .csproj — the EncryptedChat.Api/ folder also contains a stray .sln,
# so `dotnet publish EncryptedChat.Api` is ambiguous.
dotnet publish EncryptedChat.Api/EncryptedChat.csproj -c Release -o publish/api --nologo

# Obfuscar reads publish/api, writes obfuscated copy to publish/api-obf (+ Mapping.txt)
dotnet obfuscar.console obfuscar.api.xml

# Swap the obfuscated assembly back into the publish output
cp publish/api-obf/EncryptedChat.dll publish/api/EncryptedChat.dll

echo "API publish (obfuscated) ready in publish/api ; symbol map: publish/api-obf/Mapping.txt"
