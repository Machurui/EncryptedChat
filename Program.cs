using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<User>()
    .AddEntityFrameworkStores<EncryptedChatContext>();

builder.Services.AddSqlite<EncryptedChatContext>("Data source=encryptedchat.db");

builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<MessageService>();
builder.Services.AddScoped<AuthService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (context.Request.Path.Equals("/register", StringComparison.OrdinalIgnoreCase)
        && context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("This endpoint is disabled. Use /api/Auth/register instead.");
        return;
    }

    await next();
});

app.MapIdentityApi<User>();

app.MapControllers();

app.MapGet("/", () => @"Encrypted Chat API. Navigate to /swagger to open the Swagger test UI.");

app.Run();
