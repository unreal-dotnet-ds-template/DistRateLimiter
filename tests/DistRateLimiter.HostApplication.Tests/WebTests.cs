using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.Utilities;

namespace DistRateLimiter.HostApplication.Tests;

public class WebTests
{
    [Fact]
    public async Task GetWebResourceRootReturnsOkStatusCode()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.DistRateLimiter_Aspires_AppHost>(cancellationToken);

        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
            clientBuilder.AddStandardResilienceHandler();
        });

        await using var app = await appHost.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);

        // Act
        await app.ResourceNotifications
            .WaitForResourceHealthyAsync("DistRateLimiter-web-api", cancellationToken);

        using var httpClient = app.CreateHttpClient("DistRateLimiter-web-api");
        var response = await httpClient.GetAsync(new Uri("/weatherforecast/0", UriKind.Relative), cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOrleansDashboardReturnsOkStatusCode()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.DistRateLimiter_Aspires_AppHost>(cancellationToken);

        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
            clientBuilder.AddStandardResilienceHandler();
        });

        await using var app = await appHost.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);

        // Act
        await app.ResourceNotifications
            .WaitForResourceHealthyAsync("DistRateLimiter-web-orleans-silo", cancellationToken);

        using var httpClient = app.CreateHttpClient("DistRateLimiter-web-orleans-silo");
        var response = await httpClient.GetAsync(new Uri("/orleans-dashboard", UriKind.Relative), cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
