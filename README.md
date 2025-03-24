# Aspire.MongoDB.ReplicaSet
Provides the ability to create a MongoDB replica set for Aspire.

Use the extension method below to add a replica set to MongoDB when you want to specify the content path, dockerfile to override the location of the key file name

```C#
IResourceBuilder<MongoDBServerResource> WithReplicaSet(this IResourceBuilder<MongoDBServerResource> builder, string contextPath, string dockerFile, string keyFileName =  "/etc/mongo-keyfile")
```

Alternatively if you want to use the Mongo.Dockerfile included with this NuGet package, you can use the extension method below. This method will try and locate the Mongo.Dockerfile in the same directory as the executing assembly.

```C#
IResourceBuilder<MongoDBServerResource> WithReplicaSet(this IResourceBuilder<MongoDBServerResource> builder)
```

Use the extension method AddMongoReplicaSet() to add a MongoDB replica set resource to the distributed application builder.

```C#
IResourceBuilder<MongoReplicaSetResource> AddMongoReplicaSet(this IDistributedApplicationBuilder builder, string name, IResourceWithConnectionString mongoDbResource)
```

A full example can be seen below when using the Mongo.Dockerfile provided with an example API Project:

```C#
var builder = DistributedApplication.CreateBuilder(args);

var username = builder.AddParameter("mongo-user", "admin");
var password = builder.AddParameter("mongo-password", "admin");
const int port = 27017;

var mongo = builder
    .AddMongoDB("mongo", port, username, password)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithReplicaSet();

var mongodb = mongo
    .AddDatabase("mongoDatabase");

var mongoReplicaSet = builder
    .AddMongoReplicaSet("mongoDb", mongodb.Resource);

builder.AddProject<AspireMongoReplicaSet_API>("aspiremongoreplicaset-api", "http")
    .WithReference(mongodb)
    .WithReference(mongoReplicaSet)
    .WaitFor(mongodb)
    .WaitFor(mongoReplicaSet);

await builder
    .Build()
    .RunAsync();
```