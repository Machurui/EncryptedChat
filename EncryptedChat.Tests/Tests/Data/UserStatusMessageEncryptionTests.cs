using EncryptedChat.Data;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EncryptedChat.Tests;

public class UserStatusMessageEncryptionTests
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
    public async Task StatusMessage_RoundTripsThroughConverter()
    {
        string dbName = Guid.NewGuid().ToString();
        DbContextOptions<EncryptedChatContext> options = new DbContextOptionsBuilder<EncryptedChatContext>()
            .UseInMemoryDatabase(dbName).Options;
        FieldCipher cipher = Cipher();

        // Write with a cipher-enabled context (converter encrypts on save).
        await using (EncryptedChatContext ctx = new(options, cipher))
        {
            ctx.Users.Add(new User
            {
                Id = "enc-1", Name = "Enc", Email = "enc@test.com",
                NormalizedEmail = "ENC@TEST.COM", UserName = "enc@test.com",
                NormalizedUserName = "ENC@TEST.COM", StatusMessage = "top secret"
            });
            await ctx.SaveChangesAsync();
        }

        // Read back through a fresh cipher-enabled context: plaintext restored.
        await using (EncryptedChatContext ctx = new(options, cipher))
        {
            User u = await ctx.Users.FirstAsync(x => x.Id == "enc-1");
            u.StatusMessage.Should().Be("top secret");
        }
    }

    [Fact]
    public async Task StatusMessage_NullRoundTrips_NoException()
    {
        string dbName = Guid.NewGuid().ToString();
        DbContextOptions<EncryptedChatContext> options = new DbContextOptionsBuilder<EncryptedChatContext>()
            .UseInMemoryDatabase(dbName).Options;
        FieldCipher cipher = Cipher();

        await using (EncryptedChatContext ctx = new(options, cipher))
        {
            ctx.Users.Add(new User
            {
                Id = "enc-null", Name = "NoStatus", Email = "ns@test.com",
                NormalizedEmail = "NS@TEST.COM", UserName = "ns@test.com",
                NormalizedUserName = "NS@TEST.COM", StatusMessage = null
            });
            await ctx.SaveChangesAsync();
        }

        await using (EncryptedChatContext ctx = new(options, cipher))
        {
            User u = await ctx.Users.FirstAsync(x => x.Id == "enc-null");
            u.StatusMessage.Should().BeNull();
        }
    }

    [Fact]
    public void Cipher_ProducesNonPlaintextCiphertext()
    {
        // Belt-and-suspenders: the stored form is not the plaintext.
        FieldCipher cipher = Cipher();
        cipher.Encrypt("top secret", "StatusMessage").Should().NotBe("top secret");
    }
}
