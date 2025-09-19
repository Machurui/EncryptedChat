using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore; // for UseSqlite
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ---------- Services ----------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DbContext first
builder.Services.AddDbContext<EncryptedChatContext>(options =>
{
    options.UseSqlite("Data source=encryptedchat.db");
});

// Identity (still used for users, passwords, roles — but NOT for cookies)
builder.Services
    .AddIdentity<User, IdentityRole>()
    .AddEntityFrameworkStores<EncryptedChatContext>()
    .AddDefaultTokenProviders();

// ===== JWT auth =====
var jwtSection = builder.Configuration.GetSection("Jwt");
var keyBytes = Encoding.UTF8.GetBytes(jwtSection["Key"]!);
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
            IssuerSigningKey = signingKey
        };
    });

builder.Services.AddAuthorization();

// SignalR (optional, unchanged)
builder.Services.AddSignalR();

// ===== CORS (no credentials needed with Bearer tokens) =====
// Replace/trim this list to the exact origins you actually use.
var allowedOrigins = new[]
{
    "http://localhost:5183",
    "https://localhost:5183",
    "http://localhost:7276",
    "https://localhost:7276"
};

builder.Services.AddCors(o => o.AddPolicy("Client", p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
// No .AllowCredentials() for Bearer tokens
));

// App services
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IMessageService, MessageService>();

// Auth service now returns JWTs, not cookies
builder.Services.AddScoped<IAuthService, AuthService>();

// New: token generator
builder.Services.AddScoped<JwtTokenService>();

// (your email sender, unchanged)
builder.Services.AddSingleton<IEmailSender<User>, FakeEmailSender>();

var app = builder.Build();

// ---------- Pipeline ----------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseStaticFiles(); // if you serve any

app.UseRouting();

app.UseCors("Client");

app.UseAuthentication(); // JWT
app.UseAuthorization();

app.MapControllers();

// app.MapHub<ChatHub>("/chat"); // if using SignalR

app.MapGet("/", () => @"Encrypted Chat API. Navigate to /swagger to open the Swagger test UI.");

app.Run();
