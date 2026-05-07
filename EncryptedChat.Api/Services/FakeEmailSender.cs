using Microsoft.AspNetCore.Identity;
using EncryptedChat.Models;

namespace EncryptedChat.Services;

public class FakeEmailSender(IWebHostEnvironment env) : IEmailSender<User>
{
    private readonly IWebHostEnvironment _env = env;

    public Task SendConfirmationLinkAsync(User user, string email, string confirmationLink)
    {
        if (_env.IsDevelopment())
            Console.WriteLine($"[FakeEmailSender] Confirmation email to {email}");
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(User user, string email, string resetLink)
    {
        if (_env.IsDevelopment())
            Console.WriteLine($"[FakeEmailSender] Password reset link to {email}");
        return Task.CompletedTask;
    }

    public Task SendPasswordChangedConfirmationAsync(User user, string email)
    {
        if (_env.IsDevelopment())
            Console.WriteLine($"[FakeEmailSender] Password changed confirmation to {email}");
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(User user, string email, string resetCode)
    {
        if (_env.IsDevelopment())
            Console.WriteLine($"[FakeEmailSender] Password reset code to {email}");
        return Task.CompletedTask;
    }
}
