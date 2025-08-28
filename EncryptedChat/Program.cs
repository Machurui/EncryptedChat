using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Identity;
using EncryptedChat.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthorization();
builder.Services.AddIdentity<User, IdentityRole>()
    .AddEntityFrameworkStores<EncryptedChatContext>()
    .AddDefaultTokenProviders();
builder.Services.AddSignalR();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
});

builder.Services.AddCors(o => o.AddPolicy("Client", p => p
    .WithOrigins(
        "http://localhost:5183"
    )
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
));

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.None;            // cross-site
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only
    o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
});


builder.Services.AddSqlite<EncryptedChatContext>("Data source=encryptedchat.db");

builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ITeamService, TeamService>();
<<<<<<<< HEAD:EncryptedChat.Api/Program.cs
builder.Services.AddScoped<MessageService>();
========
builder.Services.AddScoped<IMessageService, MessageService>();
>>>>>>>> origin/Auth_v1.0:EncryptedChat/Program.cs
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddSingleton<IEmailSender<User>, FakeEmailSender>();

var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    string[] roleNames = { "Admin", "User", "Manager" };

    foreach (var roleName in roleNames)
    {
        var roleExist = await roleManager.RoleExistsAsync(roleName);
        if (!roleExist)
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

<<<<<<<< HEAD:EncryptedChat.Api/Program.cs
app.UseCors("Client");
========
app.MapHub<ChatHub>("/chat");
// test hub static files
app.UseStaticFiles();
>>>>>>>> origin/Auth_v1.0:EncryptedChat/Program.cs

app.MapGet("/", () => @"Encrypted Chat API. Navigate to /swagger to open the Swagger test UI.");

app.Run();
