#!/usr/bin/env bash
# Obfuscate the wwwroot JS of a PUBLISHED client (publish/client) in place.
# .NET <-> JS interop calls JS by string name (e.g. "encryptedChatCrypto.encryptAesGcm"),
# so we must NOT rename globals or object properties:
#   - renameGlobals stays false (default)  -> window.* entry-points preserved
#   - renameProperties stays false (default) -> interop method names preserved
# Only local identifiers + string-array encoding are obfuscated.
set -euo pipefail
cd "$(dirname "$0")/.."

SRC="EncryptedChat.Client/wwwroot/js"
OUT="publish/client/wwwroot/js"

[ -d "$OUT" ] || { echo "ERROR: $OUT not found — publish the client first."; exit 1; }

npx --yes javascript-obfuscator "$SRC" --output "$OUT" \
  --compact true \
  --string-array true --string-array-encoding base64 \
  --rename-globals false

# Blazor publish precompresses static assets; drop stale .gz/.br for the JS so the
# obfuscated plaintext is what gets served (static-file middleware falls back to .js).
rm -f "$OUT"/*.gz "$OUT"/*.br

echo "Obfuscated wwwroot JS → $OUT (globals + property names preserved)"
