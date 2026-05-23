using System.Net;
using System.Text;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;

namespace EncryptedChat.Tests;

public class GiphyGifServiceTests
{
    private static (GiphyGifService service, Mock<HttpMessageHandler> handlerMock) CreateService(
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
                Content = new StringContent("{\"data\":[]}", Encoding.UTF8, "application/json")
            });

        var http = new HttpClient(handlerMock.Object);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Giphy:ServiceApiKey"] = apiKey
            })
            .Build();

        var service = new GiphyGifService(http, config);
        return (service, handlerMock);
    }

    [Fact]
    public async Task SearchAsync_BuildsCorrectGiphyUrl()
    {
        var (service, handlerMock) = CreateService(apiKey: "my-secret-key");

        await service.SearchAsync("cat", 20, 0, CancellationToken.None);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.RequestUri!.Host == "api.giphy.com" &&
                req.RequestUri.AbsolutePath == "/v1/gifs/search" &&
                req.RequestUri.Query.Contains("api_key=my-secret-key") &&
                req.RequestUri.Query.Contains("q=cat") &&
                req.RequestUri.Query.Contains("limit=20") &&
                req.RequestUri.Query.Contains("rating=pg-13") &&
                req.RequestUri.Query.Contains("lang=fr")
            ),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_ReturnsParsedResults_FromValidJson()
    {
        var json = """
            {
              "data": [
                {
                  "images": {
                    "original": { "url": "https://media.giphy.com/full.gif" },
                    "fixed_width": { "url": "https://media.giphy.com/tiny.gif", "width": "200", "height": "150" }
                  }
                },
                {
                  "images": {
                    "original": { "url": "https://media.giphy.com/full2.gif" },
                    "fixed_width": { "url": "https://media.giphy.com/tiny2.gif", "width": "200", "height": "180" }
                  }
                }
              ]
            }
            """;
        var (service, _) = CreateService(response: new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var results = await service.SearchAsync("cat", 20, 0, CancellationToken.None);

        results.Should().HaveCount(2);
        results[0].Should().BeEquivalentTo(new GifResultDTO("https://media.giphy.com/full.gif", "https://media.giphy.com/tiny.gif", 200, 150));
        results[1].Should().BeEquivalentTo(new GifResultDTO("https://media.giphy.com/full2.gif", "https://media.giphy.com/tiny2.gif", 200, 180));
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmpty_WhenGiphyReturnsNoResults()
    {
        var (service, _) = CreateService(response: new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":[]}", Encoding.UTF8, "application/json")
        });

        var results = await service.SearchAsync("zzzznoresult", 20, 0, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_Throws_WhenApiKeyMissing()
    {
        var (service, _) = CreateService(apiKey: "");

        var act = async () => await service.SearchAsync("cat", 20, 0, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Giphy API key*");
    }

    [Fact]
    public async Task SearchAsync_PassesOffsetParameter()
    {
        var (service, handlerMock) = CreateService(apiKey: "my-secret-key");

        await service.SearchAsync("cat", 20, 40, CancellationToken.None);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.Query.Contains("offset=40")
            ),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task TrendingAsync_BuildsCorrectGiphyUrl()
    {
        var (service, handlerMock) = CreateService(apiKey: "my-secret-key");

        await service.TrendingAsync(20, 0, CancellationToken.None);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.RequestUri!.Host == "api.giphy.com" &&
                req.RequestUri.AbsolutePath == "/v1/gifs/trending" &&
                req.RequestUri.Query.Contains("api_key=my-secret-key") &&
                req.RequestUri.Query.Contains("limit=20") &&
                req.RequestUri.Query.Contains("offset=0") &&
                req.RequestUri.Query.Contains("rating=pg-13")
            ),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task TrendingAsync_ReturnsParsedResults_FromValidJson()
    {
        var json = """
            {
              "data": [
                {
                  "images": {
                    "original": { "url": "https://media.giphy.com/t1.gif" },
                    "fixed_width": { "url": "https://media.giphy.com/t1-tiny.gif", "width": "200", "height": "200" }
                  }
                }
              ]
            }
            """;
        var (service, _) = CreateService(response: new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var results = await service.TrendingAsync(20, 0, CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].Should().BeEquivalentTo(new GifResultDTO("https://media.giphy.com/t1.gif", "https://media.giphy.com/t1-tiny.gif", 200, 200));
    }

    [Fact]
    public async Task CategoriesAsync_BuildsCorrectGiphyUrl()
    {
        var (service, handlerMock) = CreateService(apiKey: "my-secret-key", response: new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":[]}", Encoding.UTF8, "application/json")
        });

        await service.CategoriesAsync(CancellationToken.None);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.RequestUri!.Host == "api.giphy.com" &&
                req.RequestUri.AbsolutePath == "/v1/gifs/categories" &&
                req.RequestUri.Query.Contains("api_key=my-secret-key")
            ),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task CategoriesAsync_ReturnsParsedCategories_FromValidJson()
    {
        var json = """
            {
              "data": [
                {
                  "name": "Reactions",
                  "gif": { "images": { "fixed_width_small": { "url": "https://media.giphy.com/r.gif" } } }
                },
                {
                  "name": "Love",
                  "gif": { "images": { "fixed_width_small": { "url": "https://media.giphy.com/l.gif" } } }
                }
              ]
            }
            """;
        var (service, _) = CreateService(response: new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var results = await service.CategoriesAsync(CancellationToken.None);

        results.Should().HaveCount(2);
        results[0].Should().BeEquivalentTo(new GifCategoryDTO("Reactions", "https://media.giphy.com/r.gif"));
        results[1].Should().BeEquivalentTo(new GifCategoryDTO("Love", "https://media.giphy.com/l.gif"));
    }
}
