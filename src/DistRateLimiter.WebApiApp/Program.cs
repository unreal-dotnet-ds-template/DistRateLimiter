using DistRateLimiter.Core.Features.RateLimiting;
using DistRateLimiter.WebApiApp.Models;
using Microsoft.AspNetCore.Mvc;
using Orleans.Configuration;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.AddKeyedRedisClient("DistRateLimiter-redis-orleans-clustering", (options) =>
{
    options.DisableHealthChecks = true;
});
builder.UseOrleansClient(client =>
{
    client.Configure<GatewayOptions>(options =>
    {
        options.GatewayListRefreshPeriod = TimeSpan.FromSeconds(3);
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

#region Rate Limiter API

var limiterApiGroup = app.MapGroup("api/v1/limiter").WithTags("Rate Limiter API");

limiterApiGroup.MapPost("setup", async ([FromHeader] string tenantId, [FromBody] SetupArgRecord requestBody, [FromServices] IClusterClient clusterClient) =>
{
    var key = $"{tenantId}-{requestBody.KeyId}";
    var grain = clusterClient.GetGrain<IRateLimiterGrain>(key);
    var result = await grain.SetupAsync(requestBody.MaxTokens);

    return result.IsSuccess switch
    {
        true => Results.Ok(new SetupResultRecord(requestBody.KeyId, result.Value)),
        false => Results.BadRequest(result.ErrorMessage)
    };
});

limiterApiGroup.MapPut("{keyId}/acquire-lease", async ([FromHeader] string tenantId, [FromRoute] string keyId, [FromBody] AcquireLeaseArgRecord requestArgs, [FromServices] IClusterClient clusterClient) =>
{
    var key = $"{tenantId}-{keyId}";
    var grain = clusterClient.GetGrain<IRateLimiterGrain>(key);
    var result = await grain.AcquireAsync(requestArgs.ClientId, requestArgs.RequestAmount);

    return result.IsSuccess switch
    {
        true => Results.Ok(new AcquireLeaseResultRecord(true, result.Value.GivenTokens, result.Value.RemainingTokens, string.Empty)),
        false => Results.BadRequest(result.ErrorMessage)
    };
}).WithName("AcquireLease");

limiterApiGroup.MapPut("{keyId}/return-unused-tokens", async ([FromHeader] string tenantId, [FromRoute] string keyId, [FromBody] ReturnUnusedTokensArgsRecord requestArgs, [FromServices] IClusterClient clusterClient) =>
{
    var key = $"{tenantId}-{keyId}";
    var grain = clusterClient.GetGrain<IRateLimiterGrain>(key);
    var result = await grain.ReturnUnusedTokensAsync(requestArgs.ClientId, requestArgs.TokenAmount);
    return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.ErrorMessage);
}).WithName("ReturnUnusedTokens");

limiterApiGroup.MapGet("{keyId}/get-remaining-tokens", async ([FromHeader] string tenantId, [FromRoute] string keyId, [FromServices] IClusterClient clusterClient) =>
{
    var key = $"{tenantId}-{keyId}";
    var grain = clusterClient.GetGrain<IRateLimiterGrain>(key);
    var result = await grain.GetRemainingAsync();
    return Results.Ok(result);
}).WithName("GetRemainingTokens");

#endregion

#region Test Endpoints

var testGroup1 = app.MapGroup("api/v1/test-group-1").WithTags("Test Group 1");

testGroup1.MapPost("init", async ([FromServices] IClusterClient clusterClient) =>
{
    var taskActions = Enumerable.Range(1, 100).Select(async keyId =>
    {
        var tenantId = "t1";
        var key = $"{tenantId}-{keyId}";
        var isSuccess = true;
        var message = "";
        try
        {
            var grain = clusterClient.GetGrain<IRateLimiterGrain>(key);
            var result = await grain.SetupAsync(150);
            isSuccess = result.IsSuccess;
        }
        catch(Exception ex)
        {
            isSuccess = false;
            message = ex.Message;
        }
        return new { TenantId = tenantId, KeyId = keyId, IsSuccess = isSuccess, Message = message };
    });

    var result = await Task.WhenAll(taskActions);
    return Results.Ok(result);
}).WithName("Init");

testGroup1.MapGet("get-all", async ([FromServices] IClusterClient clusterClient) =>
{
    var taskActions = Enumerable.Range(1, 100).Select(async keyId =>
    {
        var tenantId = "t1";
        var key = $"{tenantId}-{keyId}";
        var isSuccess = true;
        var remainingTokens = -1;
        var message = "";

        try
        {
            var grain = clusterClient.GetGrain<IRateLimiterGrain>(key);
            var result = await grain.GetRemainingAsync();
            remainingTokens = (int)result;
        }
        catch(Exception ex)
        {
            isSuccess = false;
            message = ex.Message;
        }
        
        return new { TenantId = tenantId, KeyId = keyId, IsSuccess = isSuccess, RemainingTokens = remainingTokens, Message = message };
    });

    var results = await Task.WhenAll(taskActions);
    return Results.Ok(results);
}).WithName("GetAll");

#endregion

app.MapDefaultEndpoints();

app.Run();
