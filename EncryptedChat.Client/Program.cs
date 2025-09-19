using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using EncryptedChat.Client.Auth;
using EncryptedChat.Client.Services;
using tailwind_4_blazor_starter;
using tailwind_4_blazor_starter.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<EncryptedChat.Client.App>("#app");

// API Endpoint
const string ApiBase = "https://localhost:7294/";

// Delegating handler that adds Authorization: Bearer <token>
builder.Services.AddTransient<BearerHandler>();

// HttpClient with BearerHandler
builder.Services.AddScoped(sp =>
{
    var bearer = sp.GetRequiredService<BearerHandler>();
    bearer.InnerHandler = new HttpClientHandler(); // safe in WASM in .NET 8

    return new HttpClient(bearer)
    {
        BaseAddress = new Uri(ApiBase)
    };
});
// TEMP: quick sanity log
builder.Services.AddScoped(sp =>
{
    var store = sp.GetRequiredService<TokenStore>();
    Console.WriteLine($"[TokenStore] initial token present: {(!string.IsNullOrWhiteSpace(store.AccessToken))}");
    return store;
});

// Program.cs (client)
builder.Services.AddAuthorizationCore();

builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<TokenStorageService>();

builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());

builder.Services.AddScoped<AuthClient>();

// Flowbite Import
builder.Services.AddScoped<IFlowbiteService, FlowbiteService>();

await builder.Build().RunAsync();

