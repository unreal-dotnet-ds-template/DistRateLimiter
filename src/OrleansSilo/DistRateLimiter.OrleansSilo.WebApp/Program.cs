using Orleans.Configuration;
using Orleans.Dashboard;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.AddServiceDefaults();
builder.AddKeyedRedisClient("DistRateLimiter-redis-orleans-clustering", (options) =>
{
    options.DisableHealthChecks = true;
});

builder.UseOrleans(siloBuilder =>
{
    siloBuilder.AddDashboard();
    siloBuilder.AddRedisGrainStorage("tokensStore");
    siloBuilder.Configure<ClusterMembershipOptions>(options =>
    {
        options.NumMissedProbesLimit = 2;
        options.NumVotesForDeathDeclaration = 1;
        options.TableRefreshTimeout = TimeSpan.FromSeconds(3);
        options.IAmAliveTablePublishTimeout = TimeSpan.FromSeconds(5);
        options.NumMissedTableIAmAliveLimit = 2;
    });
    siloBuilder.Configure<SiloMessagingOptions>(options =>
    {
        options.SystemResponseTimeout = TimeSpan.FromSeconds(5);
        options.MaxForwardCount = 3;
    });
    siloBuilder.Configure<MessagingOptions>(options =>
    {
        options.ResponseTimeout = TimeSpan.FromSeconds(5);
    });
});

builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();
app.MapOrleansDashboard("/orleans-dashboard");
app.MapGet("/", () => Results.Redirect("/orleans-dashboard"));

app.Run();
