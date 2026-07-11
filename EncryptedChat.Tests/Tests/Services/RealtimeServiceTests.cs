using System.Text.Json;
using EncryptedChat.Hubs;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace EncryptedChat.Tests;

public class RealtimeServiceTests
{
    [Fact]
    public async Task BroadcastLevelChangedAsync_SendsClientCompatibleUserId()
    {
        Guid teamId = Guid.NewGuid();
        const string userId = "user-123";
        object?[]? sentArguments = null;

        Mock<IClientProxy> group = new();
        group
            .Setup(proxy => proxy.SendCoreAsync(
                "LevelChanged",
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((_, arguments, _) => sentArguments = arguments)
            .Returns(Task.CompletedTask);

        Mock<IHubClients> clients = new();
        clients.Setup(value => value.Group($"team-{teamId}")).Returns(group.Object);

        Mock<IHubContext<ChatHub>> hubContext = new();
        hubContext.Setup(value => value.Clients).Returns(clients.Object);

        RealtimeService service = new(hubContext.Object, Mock.Of<ILogger<RealtimeService>>());

        await service.BroadcastLevelChangedAsync(userId, 2, [teamId]);

        sentArguments.Should().NotBeNull().And.HaveCount(1);
        RealtimeService.LevelChangedDTO payload = sentArguments![0]
            .Should().BeOfType<RealtimeService.LevelChangedDTO>().Subject;
        payload.UserId.Should().Be(userId);
        payload.Level.Should().Be(2);

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(
            payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        json.RootElement.TryGetProperty("userId", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("id", out _).Should().BeFalse();
    }
}
