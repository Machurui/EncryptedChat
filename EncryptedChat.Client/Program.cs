using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using EncryptedChat.Client.Auth;
using EncryptedChat.Client.Services;
using tailwind_4_blazor_starter;
using tailwind_4_blazor_starter.Services;
using Sentry.AspNetCore.Blazor.WebAssembly;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<EncryptedChat.Client.App>("#app");

// ---------- Observability (Sentry) ----------
// DSN from wwwroot/appsettings.json (empty ⇒ SDK disabled). Aggressive scrubbing (E2E app).
builder.UseSentry(options =>
{
    options.Dsn = builder.Configuration["Sentry:Dsn"] ?? string.Empty;
    options.Environment = builder.HostEnvironment.Environment;
    options.SendDefaultPii = false;
    options.SetBeforeSend((sentryEvent, _) =>
        EncryptedChat.Client.Observability.ClientSentryScrubbing.ScrubEvent(sentryEvent));
    options.SetBeforeBreadcrumb((breadcrumb, _) =>
        EncryptedChat.Client.Observability.ClientSentryScrubbing.ScrubBreadcrumb(breadcrumb));
    // Tracing (perf). Sample rate from config (0 ⇒ tracing off). Invariant parse (no extra using).
    options.TracesSampleRate =
        double.TryParse(builder.Configuration["Sentry:TracesSampleRate"],
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double rate)
            ? rate : 0.0;
});

// API endpoint: configurable via wwwroot/appsettings.json ("ApiBaseUrl"). Empty ⇒ same
// origin as the WASM host (BaseAddress) — the Docker case, where nginx reverse-proxies
// /api + /hubs to the backend. Dev (appsettings.Development.json) keeps the cross-origin
// API URL (https://localhost:7294/).
string configuredApiBase = builder.Configuration["ApiBaseUrl"] ?? string.Empty;
string apiBase = string.IsNullOrWhiteSpace(configuredApiBase)
    ? builder.HostEnvironment.BaseAddress
    : configuredApiBase;

builder.Services.AddTransient<CookieHandler>();
builder.Services.AddScoped(sp =>
{
    CookieHandler handler = sp.GetRequiredService<CookieHandler>();
    return new HttpClient(handler)
    {
        BaseAddress = new Uri(apiBase)
    };
});

// Program.cs (client)
builder.Services.AddAuthorizationCore();

builder.Services.AddScoped<CookieAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CookieAuthStateProvider>());

builder.Services.AddScoped<AuthClient>();

builder.Services.AddScoped<UserClient>();

builder.Services.AddScoped<TeamClient>();
builder.Services.AddScoped<InviteClient>();

builder.Services.AddScoped<ChatClient>();

builder.Services.AddScoped<AttachmentClient>();
builder.Services.AddScoped<FriendClient>();
builder.Services.AddScoped<SecurityClient>();
builder.Services.AddScoped(sp => new PinnedMessageClient(sp.GetRequiredService<HttpClient>()));
builder.Services.AddScoped<BubbleColorClient>();
builder.Services.AddScoped<GifClient>();
builder.Services.AddScoped<RecentGifsService>();
builder.Services.AddScoped<GifVaultService>();
builder.Services.AddScoped<RecentNameColorsService>();

// E2E crypto foundation
builder.Services.AddScoped<EncryptedChat.Client.Services.Crypto.CryptoService>();
builder.Services.AddScoped<EncryptedChat.Client.Services.Crypto.KeyVaultService>();
builder.Services.AddSingleton<EncryptedChat.Client.Services.Crypto.TeamKeyCacheService>();
builder.Services.AddScoped<EncryptedChat.Client.Services.Crypto.BootstrapKeyService>();
builder.Services.AddScoped<EncryptedChat.Client.Services.Crypto.IPinStore, EncryptedChat.Client.Services.Crypto.IndexedDbPinStore>();
builder.Services.AddScoped<EncryptedChat.Client.Services.Crypto.IKeyVerificationService, EncryptedChat.Client.Services.Crypto.KeyVerificationService>();

// Flowbite Import
builder.Services.AddScoped<IFlowbiteService, FlowbiteService>();

await builder.Build().RunAsync();

