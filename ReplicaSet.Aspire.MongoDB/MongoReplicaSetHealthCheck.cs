using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ReplicaSet.Aspire.MongoDB;

public class MongoReplicaSetHealthCheck(MongoClientSettings mongoClientSettings) : IHealthCheck
{
    /// <inheritdoc cref="IHealthCheck.CheckHealthAsync" />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = new())
    {
        var mongoClient = new MongoClient(mongoClientSettings);
        var adminDb = mongoClient.GetDatabase("admin");

        try
        {
            var initiateCmd = new BsonDocument { { "replSetInitiate", new BsonDocument() } };
            await adminDb.RunCommandAsync<BsonDocument>(initiateCmd, cancellationToken: cancellationToken);
        }
        catch (MongoCommandException ex)
        {
            if (ex.CodeName != "AlreadyInitialized")
            {
                return HealthCheckResult.Unhealthy("Failed to initialize MongoDB replica set.", ex);
            }
        }

        try
        {
            var isMasterCmd = new BsonDocument { { "isMaster", 1 } };
            var result = await adminDb.RunCommandAsync<BsonDocument>(isMasterCmd, cancellationToken: cancellationToken);
            if (result.TryGetValue("ismaster", out BsonValue isMaster) && isMaster.AsBoolean)
            {
                return HealthCheckResult.Healthy();
            }

            return HealthCheckResult.Unhealthy("MongoDB is not the primary node.");
        }
        catch (MongoCommandException ex)
        {
            return HealthCheckResult.Unhealthy("Failed to determine MongoDB primary node.", ex);
        }
    }
}