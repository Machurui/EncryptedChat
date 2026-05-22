using System.Net;
using System.Text;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;

namespace EncryptedChat.Tests;

public class TenorGifServiceTests
{
    private static (TenorGifService service, Mock<HttpMessageHandler> handlerMock) CreateService(
        string apiKey = "test-key-123",
        HttpResponseMessage? response = null)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response ?? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"results\":[]}", Encoding.UTF8, "application/json")
            });

        var http = new HttpClient(handlerMock.Object);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gifs:TenorApiKey"] = apiKey
            })
            .Build();

        var service = new TenorGifService(http, config);
        return (service, handlerMock);
    }

    [Fact]
    public async Task SearchAsync_BuildsCorrectTenorUrl()
    {
        var (service, handlerMock) = CreateService(apiKey: "my-secret-key");

        await service.SearchAsync("cat", 20, CancellationToken.None);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.RequestUri!.Host == "tenor.googleapis.com" &&
                req.RequestUri.AbsolutePath == "/v2/search" &&
                req.RequestUri.Query.Contains("key=my-secret-key") &&
                req.RequestUri.Query.Contains("q=cat") &&
                req.RequestUri.Query.Contains("limit=20") &&
                req.RequestUri.Query.Contains("locale=fr_FR") &&
                req.RequestUri.Query.Contains("contentfilter=medium") &&
                req.RequestUri.Query.Contains("media_filter=gif%2Ctinygif")
            ),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_ReturnsParsedResults_FromValidJson()
    {
        var json = """
            {
              "results": [
                {
                  "media_formats": {
                    "gif": { "url": "https://media.tenor.com/full.gif" },
                    "tinygif": { "url": "https://media.tenor.com/tiny.gif" }
                  }
                },
                {
                  "media_formats": {
                    "gif": { "url": "https://media.tenor.com/full2.gif" },
                    "tinygif": { "url": "https://media.tenor.com/tiny2.gif" }
                  }
                }
              ]
            }
            """;
        var (service, _) = CreateService(response: new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var results = await service.SearchAsync("cat", 20, CancellationToken.None);

        results.Should().HaveCount(2);
        results[0].Should().BeEquivalentTo(new GifResultDTO("https://media.tenor.com/full.gif", "https://media.tenor.com/tiny.gif"));
        results[1].Should().BeEquivalentTo(new GifResultDTO("https://media.tenor.com/full2.gif", "https://media.tenor.com/tiny2.gif"));
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmpty_WhenTenorReturnsNoResults()
    {
        var (service, _) = CreateService(response: new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"results\":[]}", Encoding.UTF8, "application/json")
        });

        var results = await service.SearchAsync("zzzznoresult", 20, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_Throws_WhenApiKeyMissing()
    {
        var (service, _) = CreateService(apiKey: "");

        var act = async () => await service.SearchAsync("cat", 20, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Tenor API key*");
    }
}
