#!/bin/sh
set -eu

APPSETTINGS_PATH="/usr/share/nginx/html/appsettings.json"

json_escape() {
    printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g'
}

normalize_sample_rate() {
    case "${1:-}" in
        ''|*[!0-9.]*)
            printf '0.0'
            ;;
        *)
            printf '%s' "$1"
            ;;
    esac
}

API_BASE_URL_VALUE="$(json_escape "${API_BASE_URL:-}")"
SENTRY_DSN_VALUE="$(json_escape "${SENTRY_DSN:-}")"
SENTRY_TRACES_SAMPLE_RATE_VALUE="$(normalize_sample_rate "${SENTRY_TRACES_SAMPLE_RATE:-0.0}")"

cat > "$APPSETTINGS_PATH" <<EOF
{
  "ApiBaseUrl": "$API_BASE_URL_VALUE",
  "Sentry": {
    "Dsn": "$SENTRY_DSN_VALUE",
    "TracesSampleRate": $SENTRY_TRACES_SAMPLE_RATE_VALUE
  }
}
EOF
