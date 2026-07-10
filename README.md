# EncryptedChat

EncryptedChat is a private-first chat application built with an ASP.NET Core API,
a Blazor WebAssembly client, SQL Server, SignalR, and client-side cryptography.

The server handles identity, sessions, team membership, message transport,
metadata encryption, storage, rate limiting, and observability. Message content
is encrypted and signed in the browser before it is sent.

## Main Features

- End-to-end encrypted team messages using browser WebCrypto primitives.
- Message signatures and safety-number/key-verification flows.
- Team key sharing, missing-share detection, invite links, DMs, read state, mute
  state, pinned messages, presence, and owner/admin/member roles.
- Recent team-management hardening: owners/admins cannot remove themselves,
  owners are protected, admins can remove regular members only, and the last
  admin cannot be removed.
- Cookie-backed JWT authentication with refresh tokens, session tracking, and
  session revocation.
- Account recovery via a 12-word recovery phrase hashed with PBKDF2.
- Server-side encryption for sensitive searchable metadata with blind indexes.
- Attachments with MIME validation, upload limits, per-team storage limits, and
  orphan cleanup.
- Friends, user search, profiles, avatars, status messages, bubble colors, and
  leveling.
- GIF search through a Giphy proxy with in-memory caching and a user GIF vault.
- Sentry hooks on API and client with event/breadcrumb scrubbing.
- Docker and production compose files for API, Blazor client, SQL Server, and
  Caddy reverse proxy.

## Tech Stack

- .NET 8
- ASP.NET Core Web API
- Blazor WebAssembly
- Entity Framework Core with SQL Server
- ASP.NET Core Identity
- SignalR
- Tailwind CSS 4 and Flowbite
- xUnit, FluentAssertions, Moq, TestHost, EF Core InMemory
- Docker Compose, Caddy, Sentry

## Repository Layout

```text
EncryptedChat.Api/       ASP.NET Core API, EF Core, SignalR, services, migrations
EncryptedChat.Client/    Blazor WebAssembly client and browser crypto bridge
EncryptedChat.Tests/     Unit and integration tests
scripts/                 Publish and obfuscation helper scripts
docker-compose.yml       Local container stack
docker-compose.prod.yml  Production container stack
Caddyfile                Production reverse-proxy config
```

## Local Development

### Prerequisites

- .NET 8 SDK
- Node.js and npm
- SQL Server, or Docker for the SQL Server container
- EF Core CLI tools, if you run migrations manually:

```bash
dotnet tool install --global dotnet-ef
```

### Configure the API

The API requires a SQL Server connection string, JWT settings, and a 32-byte
AES key for server-side field encryption.

Recommended local setup uses user secrets:

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,1433;Database=EncryptedChat;User Id=sa;Password=<password>;TrustServerCertificate=True;" --project EncryptedChat.Api
dotnet user-secrets set "Jwt:Issuer" "EncryptedChat" --project EncryptedChat.Api
dotnet user-secrets set "Jwt:Audience" "EncryptedChat.Client" --project EncryptedChat.Api
dotnet user-secrets set "Jwt:Key" "<at-least-32-characters>" --project EncryptedChat.Api
dotnet user-secrets set "Encryption:Key" "<base64-encoded-32-byte-key>" --project EncryptedChat.Api
dotnet user-secrets set "Giphy:ServiceApiKey" "<optional-giphy-key>" --project EncryptedChat.Api
```

Generate an encryption key with:

```bash
openssl rand -base64 32
```

You can also copy the development example and fill in local values:

```bash
cp EncryptedChat.Api/appsettings.Development.json.example EncryptedChat.Api/appsettings.Development.json
```

The Blazor client reads its API endpoint from
`EncryptedChat.Client/wwwroot/appsettings*.json`. In development it targets:

```text
https://localhost:7294/
```

### Database

Apply migrations after configuring the connection string:

```bash
dotnet ef database update --project EncryptedChat.Api/EncryptedChat.csproj
```

### Run Locally

Start the API:

```bash
dotnet run --project EncryptedChat.Api/EncryptedChat.csproj --launch-profile https
```

Start the client:

```bash
dotnet watch --project EncryptedChat.Client/EncryptedChat.Client.csproj run --launch-profile https
```

For Tailwind development:

```bash
cd EncryptedChat.Client
npm install
npx tailwindcss -i wwwroot/css/input.css -o wwwroot/css/output.css --watch
```

Useful local URLs:

- API Swagger: `https://localhost:7294/swagger`
- API HTTP fallback: `http://localhost:5130`
- Client HTTPS: `https://localhost:7174`
- Client HTTP fallback: `http://localhost:5183`

## Docker

The local compose stack builds the API and client images and runs SQL Server:

```bash
MSSQL_SA_PASSWORD="<strong-password>" \
JWT_KEY="<at-least-32-characters>" \
JWT_ISSUER="EncryptedChat" \
JWT_AUDIENCE="EncryptedChat.Client" \
ENCRYPTION_KEY="$(openssl rand -base64 32)" \
GIPHY_API_KEY="" \
docker compose up --build
```

The web container is exposed on:

```text
http://localhost:8080
```

The production compose file expects prebuilt GHCR images and Caddy:

```bash
IMAGE_TAG="<tag>" \
MSSQL_TAG="2025-latest" \
DOMAIN="chat.example.com" \
MSSQL_SA_PASSWORD="<strong-password>" \
JWT_KEY="<at-least-32-characters>" \
JWT_ISSUER="EncryptedChat" \
JWT_AUDIENCE="EncryptedChat.Client" \
ENCRYPTION_KEY="<base64-encoded-32-byte-key>" \
GIPHY_API_KEY="" \
SENTRY_DSN="" \
docker compose -f docker-compose.prod.yml up -d
```

## Kubernetes / Rancher

The API validates its required configuration before it opens port 8080. For a
Kubernetes workload, provide secrets through a Kubernetes `Secret`; .NET maps
the double underscore in an environment variable to a configuration colon.

| Environment variable | Required | Purpose |
| --- | --- | --- |
| `ConnectionStrings__Default` | Yes | Complete SQL Server connection string |
| `Jwt__Key` | Yes | Random JWT signing key, at least 32 UTF-8 bytes |
| `Encryption__Key` | Yes | Stable base64-encoded 32-byte field-encryption key |
| `Jwt__Issuer` | No | Defaults to `EncryptedChat` |
| `Jwt__Audience` | No | Defaults to `EncryptedChat.Client` |
| `RunMigrationsOnStartup` | Deployment decision | Set to `true` on exactly one API replica |
| `DisableHttpsRedirection` | Behind an HTTP proxy | Set to `true` when TLS terminates at Cloudflare/Ingress |
| `Cookies__Secure` | No | Keep `true` for the public HTTPS endpoint |
| `Cookies__SameSite` | No | Use `Lax` for the same-origin web/API setup |
| `Giphy__ServiceApiKey` | No | Enables GIF search |
| `Sentry__Dsn` | No | Enables API telemetry |

Example API workload fragment (the referenced Secret must already exist):

```yaml
spec:
  replicas: 1
  strategy:
    type: Recreate
  template:
    spec:
      containers:
        - name: api
          image: ghcr.io/machurui/encryptedchat-api:<immutable-tag>
          ports:
            - name: http
              containerPort: 8080
          envFrom:
            - secretRef:
                name: encryptedchat-api-secrets
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: Production
            - name: RunMigrationsOnStartup
              value: "true"
            - name: DisableHttpsRedirection
              value: "true"
            - name: Cookies__Secure
              value: "true"
            - name: Cookies__SameSite
              value: Lax
          startupProbe:
            httpGet:
              path: /health
              port: http
            periodSeconds: 5
            failureThreshold: 60
```

The SQL login in `ConnectionStrings__Default` needs schema-change permissions
while migrations run. Check the API logs for `Database migrations are up to
date`, then verify the `__EFMigrationsHistory` table. Before scaling the API
beyond one replica, disable startup migrations and apply them once as a
deployment step.

## Tests

Run the full test suite:

```bash
dotnet test EncryptedChat.sln
```

The test project covers services, controllers, hubs, crypto helpers, storage,
rate limiting, session handling, recovery, Sentry scrubbing, Dockerfile checks,
and team-permission behavior.

## Publishing and Obfuscation

API publish with Obfuscar:

```bash
./scripts/publish-api-obfuscated.sh
```

Client publish with JavaScript obfuscation:

```bash
./scripts/publish-client-obfuscated.sh
```

Generated `publish/` output and obfuscation maps should stay out of source
control.

## Security Notes

- Do not commit real connection strings, JWT secrets, encryption keys, Sentry
  DSNs, or Giphy keys.
- `Encryption:Key` must be valid base64 for exactly 32 bytes.
- Authentication cookies are HTTP-only. Cookie `Secure` and `SameSite` behavior
  is configurable for dev, local Docker, and production proxy setups.
- Rate limiting is applied to user lookup, attachment upload, and account
  recovery.
- Sentry scrubbing removes sensitive request and auth data before events leave
  the app.

## License

This project is licensed under a Non-Commercial License.
You may use, copy, and modify the code for personal, educational, or
non-commercial purposes.
For commercial use, please contact francois.serra00@gmail.com to discuss
licensing.
