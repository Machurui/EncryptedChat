using EncryptedChat.Data;
using EncryptedChat.Models;
using EncryptedChat.Services;
using EncryptedChat.Hubs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ---------- Services ----------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<EncryptedChatContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("Connection string 'Default' is empty.");

    options.UseSqlServer(connectionString);
});

// Identity
builder.Services
    .AddIdentity<User, IdentityRole>()
    .AddEntityFrameworkStores<EncryptedChatContext>()
    .AddDefaultTokenProviders();

// ===== JWT auth =====
var jwtSection = builder.Configuration.GetSection("Jwt");
var keyValue = jwtSection["Key"];
Console.WriteLine($"[JWT CONFIG] Issuer={jwtSection["Issuer"]}, Audience={jwtSection["Audience"]}, KeyLength={keyValue?.Length ?? 0}");

var keyBytes = Encoding.UTF8.GetBytes(keyValue!);
var signingKey = new SymmetricSecurityKey(keyBytes);


builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = signingKey,
        };

        // Read JWT from cookie or query string (for SignalR WebSocket)
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                // For SignalR WebSocket connections, token comes in query string
                var path = ctx.HttpContext.Request.Path;
                if (path.StartsWithSegments("/hubs"))
                {
                    var accessToken = ctx.Request.Query["access_token"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        ctx.Token = accessToken;
                        return Task.CompletedTask;
                    }
                }

                // For HTTP requests, read from cookie
                if (string.IsNullOrEmpty(ctx.Token) &&
                    ctx.Request.Cookies.TryGetValue("ec.accessToken", out var cookieToken))
                {
                    ctx.Token = cookieToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// SignalR
builder.Services.AddSignalR();

// ===== CORS =====
var allowedOrigins = new[]
{
    "http://localhost:5183",
    "https://localhost:5183",
    "http://localhost:7174",
    "https://localhost:7174",
    "http://localhost:7276",
    "https://localhost:7276"
};

builder.Services.AddCors(o => o.AddPolicy("Client", p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
));

// App services
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IMessageService, MessageService>();

// Auth service
builder.Services.AddScoped<IAuthService, AuthService>();

// Token generator
builder.Services.AddScoped<JwtTokenService>();

builder.Services.AddSingleton<IEmailSender<User>, FakeEmailSender>();

var app = builder.Build();

// ---------- Pipeline ----------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseCors("Client");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHub<ChatHub>("/hubs/chat");

app.MapGet("/", () => @"Encrypted Chat API. Navigate to /swagger to open the Swagger test UI.");
app.Run();
