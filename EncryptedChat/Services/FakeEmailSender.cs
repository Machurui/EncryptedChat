using Microsoft.AspNetCore.Identity;
using EncryptedChat.Models;

namespace EncryptedChat.Services;

public class FakeEmailSender : IEmailSender<User>
{
    public Task SendConfirmationLinkAsync(User user, string email, string confirmationLink)
    {
        Console.WriteLine($"[FakeEmailSender] Confirmation email sent to {email}: {confirmationLink}");
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(User user, string email, string resetLink)
    {
        Console.WriteLine($"[FakeEmailSender] Password reset link sent to {email}: {resetLink}");
        return Task.CompletedTask;
    }

    public Task SendPasswordChangedConfirmationAsync(User user, string email)
    {
        Console.WriteLine($"[FakeEmailSender] Password changed confirmation sent to {email}");
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(User user, string email, string resetCode)
    {
        Console.WriteLine($"[FakeEmailSender] Password reset code sent to {email}: {resetCode}");
        return Task.CompletedTask;
    }
}
