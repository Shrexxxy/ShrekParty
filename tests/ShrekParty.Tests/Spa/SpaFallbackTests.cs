using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Hosting;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Spa;

public class SpaFallbackTests : IDisposable
{
    private const string IndexHtml = "<!doctype html><title>Болото</title>";

    private readonly SpaFactory _factory = new();

    [Theory]
    [InlineData("/tv")]
    [InlineData("/admin")]
    [InlineData("/register")]
    public async Task Client_routes_get_index_html(string path)
    {
        var response = await TestClients.Anonymous(_factory).GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        (await response.Content.ReadAsStringAsync()).Should().Be(IndexHtml);
    }

    [Fact]
    public async Task Index_is_not_cached()
    {
        var response = await TestClients.Anonymous(_factory).GetAsync("/tv");

        response.Headers.CacheControl!.NoCache.Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/unknown")]
    [InlineData("/api/registration/nope")]
    [InlineData("/media/not-a-guid")]
    public async Task Unknown_api_and_media_return_404_not_index(string path)
    {
        var response = await TestClients.Anonymous(_factory).GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Static_assets_are_served()
    {
        var response = await TestClients.Anonymous(_factory).GetAsync("/assets/app.js");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("console.log('ok')");
    }

    public void Dispose() => _factory.Dispose();

    /// <summary>Хост со своей папкой статики вместо собранного фронта.</summary>
    private sealed class SpaFactory : AppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            var webRoot = Path.Combine(DataDir, "www");
            Directory.CreateDirectory(Path.Combine(webRoot, "assets"));
            File.WriteAllText(Path.Combine(webRoot, "index.html"), IndexHtml);
            File.WriteAllText(Path.Combine(webRoot, "assets", "app.js"), "console.log('ok')");
            builder.UseWebRoot(webRoot);
        }
    }
}
