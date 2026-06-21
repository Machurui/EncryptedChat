using EncryptedChat.Data;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EncryptedChat.Tests;

public class UserEmailEncryptionTests
{
    private const string TestKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    private static FieldCipher Cipher()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Encryption:Key"] = TestKey })
            .Build();
        return new FieldCipher(config);
    }

    [Fact]
    public async Task Email_And_UserName_RoundTripThroughConverter()
    {
        string dbName = Guid.NewGuid().ToString();
        DbContextOptions<EncryptedChatContext> options = new DbContextOptionsBuilder<EncryptedChatContext>()
            .UseInMemoryDatabase(dbName).Options;
        FieldCipher cipher = Cipher();

        // Write through a cipher-enabled context (converter encrypts on save).
        await using (EncryptedChatContext ctx = new(options, cipher))
        {
            ctx.Users.Add(new User
            {
                Id = "ee-1", Name = "Enc",
                Email = "enc@test.com", UserName = "enc@test.com"
            });
            await ctx.SaveChangesAsync();
        }

        // Read back: Email/UserName decrypted transparently.
        await using (EncryptedChatContext ctx = new(options, cipher))
        {
            User u = await ctx.Users.FirstAsync(x => x.Id == "ee-1");
            u.Email.Should().Be("enc@test.com");
            u.UserName.Should().Be("enc@test.com");
        }
    }

    [Fact]
    public void Cipher_EmailCiphertext_IsNotPlaintext()
    {
        // Belt-and-suspenders: the stored form is not the plaintext.
        Cipher().Encrypt("enc@test.com", "Email").Should().NotBe("enc@test.com");
    }
}
