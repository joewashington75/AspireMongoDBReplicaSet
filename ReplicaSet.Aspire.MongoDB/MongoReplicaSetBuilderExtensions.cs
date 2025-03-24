using System.Reflection;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;

namespace ReplicaSet.Aspire.MongoDB;

public static class MongoReplicaSetBuilderExtensions
{
    /// <summary>
    /// Configures the MongoDB server to use a replica set.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/>.</param>
    /// <param name="contextPath">Path to where the docker file is</param>
    /// <param name="dockerFile">Name of the dockerfile to use</param>
    /// <param name="keyFileName">Name of key file to be used</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{MongoDBServerResource}"/>.</returns>
    public static IResourceBuilder<MongoDBServerResource> WithReplicaSet(this IResourceBuilder<MongoDBServerResource> builder, string contextPath, string dockerFile, string keyFileName = "/etc/mongo-keyfile")
    {
        return builder
            .WithDockerfile(contextPath, dockerFile)
            .WithArgs("--replSet", "rs0", "--bind_ip_all", "--keyFile", keyFileName);
    }

    /// <summary>
    /// Configures the MongoDB server to use a replica set. This will use a default dockerfile provided to create the keyfile and run mongo by looking for the Mongo.Dockerfile in the same directory as the executing assembly.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{MongoDBServerResource}"/>.</returns>
    public static IResourceBuilder<MongoDBServerResource> WithReplicaSet(this IResourceBuilder<MongoDBServerResource> builder)
    {
        var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        return builder
            .WithDockerfile(path!, "Mongo.Dockerfile")
            .WithArgs("--replSet", "rs0", "--bind_ip_all", "--keyFile", "/etc/mongo-keyfile");
    }

    /// <summary>
    /// Adds a MongoDB replica set resource to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="mongoDbResource">The <see cref="mongoDbResource "/> to use for the MongoDB server.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<MongoReplicaSetResource> AddMongoReplicaSet(
        this IDistributedApplicationBuilder builder,
        string name,
        IResourceWithConnectionString mongoDbResource)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(mongoDbResource);

        var mongoDbServerResource = mongoDbResource switch
        {
            MongoDBDatabaseResource dbResource => dbResource.Parent,
            _ => mongoDbResource
        };

        var mongoReplicaSetResource = new MongoReplicaSetResource(name, mongoDbServerResource);

        MongoClientSettings? mongoClientSettings = null;

        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(mongoDbServerResource,
            async (@event, ct) =>
            {
                var connectionString =
                    await mongoReplicaSetResource.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false) ??
                    throw new DistributedApplicationException(
                        $"ConnectionStringAvailableEvent was published for the '{mongoReplicaSetResource.Name}' resource but the connection string was null.");

                var options = MongoClientSettings.FromConnectionString(connectionString);
                options.LoggingSettings = new LoggingSettings(@event.Services.GetRequiredService<ILoggerFactory>());

                mongoClientSettings = options;
            });

        // the mongodb health check fails to connect because it has not the correct settings for the replica set
        foreach (var annotation in mongoDbServerResource.Annotations.OfType<HealthCheckAnnotation>().ToList())
        {
            mongoDbServerResource.Annotations.Remove(annotation);

            builder.Services.Configure<HealthCheckServiceOptions>(options =>
            {
                var mongoDbServerHealthCheck = options.Registrations
                    .FirstOrDefault(x => x.Name == annotation.Key);

                if (mongoDbServerHealthCheck is not null)
                {
                    options.Registrations.Remove(mongoDbServerHealthCheck);
                }
            });
        }

        var healthCheckKey = $"{name}_check";
        builder.Services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                healthCheckKey,
                sp => new MongoReplicaSetHealthCheck(mongoClientSettings!),
                failureStatus: null,
                tags: null,
                timeout: null));

        return builder
            .AddResource(mongoReplicaSetResource)
            .WithHealthCheck(healthCheckKey)
            .WithConnectionStringRedirection(mongoDbServerResource);
    }
}