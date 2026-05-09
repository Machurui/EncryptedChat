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

// HttpClient with cookie credentials for cross-origin requests
builder.Services.AddTransient<CookieHandler>();
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<CookieHandler>();
    return new HttpClient(handler)
    {
        BaseAddress = new Uri(ApiBase)
    };
});

// Program.cs (client)
builder.Services.AddAuthorizationCore();

builder.Services.AddScoped<CookieAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CookieAuthStateProvider>());

builder.Services.AddScoped<AuthClient>();

builder.Services.AddScoped<UserClient>();

builder.Services.AddScoped<TeamClient>();

builder.Services.AddScoped<ChatClient>();

builder.Services.AddScoped<AttachmentClient>();

// Flowbite Import
builder.Services.AddScoped<IFlowbiteService, FlowbiteService>();

await builder.Build().RunAsync();

