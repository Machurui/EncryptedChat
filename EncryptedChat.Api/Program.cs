using EncryptedChat.Data;
using EncryptedChat.Models;
using EncryptedChat.Services;
using EncryptedChat.Hubs;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// File upload limits
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 26_214_400; // 25 MB
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 26_214_400; // 25 MB
});

// ---------- Services ----------
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressMapClientErrors = true;
    })
    .AddJsonOptions(options =>
    {
        options.AllowInputFormatterExceptionMessages = false;
    });


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

// Disable cookie redirects for API
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

// ===== JWT auth =====
string jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT key is not configured.");
string jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT issuer is not configured.");
string jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT audience is not configured.");

byte[] keyBytes = Encoding.UTF8.GetBytes(jwtKey);
SymmetricSecurityKey signingKey = new(keyBytes);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = signingKey
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                PathString path = context.HttpContext.Request.Path;
                if (path.StartsWithSegments("/hubs"))
                {
                    string? accessToken = context.Request.Query["access_token"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        context.Token = accessToken;
                        return Task.CompletedTask;
                    }
                }

                if (string.IsNullOrEmpty(context.Token) &&
                    context.Request.Cookies.TryGetValue("ec.accessToken", out string? cookieToken))
                {
                    context.Token = cookieToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("UserLookup", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("AttachmentUpload", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// SignalR
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, SignalRUserIdProvider>();

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
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IPinnedMessageService, PinnedMessageService>();
builder.Services.AddScoped<IFriendService, FriendService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IRecoveryService, RecoveryService>();
builder.Services.AddScoped<IRealtimeService, RealtimeService>();

// Auth service
builder.Services.AddScoped<IAuthService, AuthService>();

// Rate limiting (anti-spam)
builder.Services.AddSingleton<IRateLimitService, RateLimitService>();
builder.Services.AddHostedService<RateLimitCleanupService>();

// Presence tracking (SignalR connection state)
builder.Services.AddSingleton<IPresenceService, PresenceService>();

// Token generator
builder.Services.AddScoped<JwtTokenService>();

builder.Services.AddSingleton<ICryptoService, CryptoService>();

// File storage
builder.Services.Configure<FileStorageOptions>(
    builder.Configuration.GetSection("FileStorage"));
builder.Services.AddSingleton<IFileStorageService, FileStorageService>();
builder.Services.AddSingleton<MimeTypeValidator>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();

// GIF search (Tenor v2 proxy)
builder.Services.AddHttpClient<IGifService, TenorGifService>();

builder.Services.AddSingleton<IEmailSender<User>, FakeEmailSender>();

builder.Services.AddHostedService<MessageCleanupService>();

var app = builder.Build();

// ---------- Pipeline ----------

// Global exception handler - returns consistent error format, no technical details
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        IExceptionHandlerFeature? exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        Exception? exception = exceptionFeature?.Error;

        ILogger<Program> logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, "Unhandled exception occurred");

        int statusCode = exception switch
        {
            BadHttpRequestException => StatusCodes.Status400BadRequest,
            System.Text.Json.JsonException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        bool isDev = context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment();
        string message = statusCode == StatusCodes.Status400BadRequest
            ? "Invalid request"
            : isDev && exception != null ? exception.Message : "An error occurred";

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { Message = message });
    });
});

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
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.MapHub<ChatHub>("/hubs/chat");

app.MapGet("/", () => @"Encrypted Chat API. Navigate to /swagger to open the Swagger test UI.");
app.Run();
