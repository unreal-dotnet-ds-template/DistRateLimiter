var builder = DistributedApplication.CreateBuilder(args);

// orleans storages for clustering and grain storage
var redisOrleansClustering = builder.AddRedis("DistRateLimiter-redis-orleans-clustering");

// orleans cluster
var orleans = builder.AddOrleans("DistRateLimiter-orleansCluster")
    .WithClustering(redisOrleansClustering)
    .WithGrainStorage("tokensStore", redisOrleansClustering);
    
// orleans silo -> backend. you run logic here.
var webOrleansSilo = builder.AddProject<Projects.DistRateLimiter_OrleansSilo_WebApp>("DistRateLimiter-web-orleans-silo")
    .WithReference(orleans)
    .WithHttpHealthCheck("/health", endpointName: "http")
    .WaitFor(redisOrleansClustering)
    .WithReplicas(4);

// orleans client -> frontend. you call grains here.
var webApi = builder.AddProject<Projects.DistRateLimiter_WebApiApp>("DistRateLimiter-web-api")
    .WithReference(orleans.AsClient()) // client-only reference
    .WithHttpHealthCheck("/health", endpointName: "http")
    .WaitFor(webOrleansSilo)
    .WithReplicas(1);

builder.Build().Run();
