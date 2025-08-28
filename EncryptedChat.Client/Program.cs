using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using EncryptedChat.Client.Auth;
using EncryptedChat.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<EncryptedChat.Client.App>("#app");

// API Endpoint
const string ApiBase = "https://localhost:7294/";

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(ApiBase)
});

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<CookieAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CookieAuthStateProvider>());

// login/logout/refresh wrapper
builder.Services.AddScoped<AuthClient>();

await builder.Build().RunAsync();
