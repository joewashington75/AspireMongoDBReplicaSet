using Aspire.Hosting.ApplicationModel;

namespace ReplicaSet.Aspire.MongoDB;

public class MongoReplicaSetResource(string name, IResourceWithConnectionString parent)
    : Resource(name), IResourceWithConnectionString, IResourceWithParent<IResourceWithConnectionString>
{
    /// <inheritdoc cref="IResourceWithConnectionString.ConnectionStringExpression"/>
    public ReferenceExpression ConnectionStringExpression
    {
        get
        {
            var connectionStringAnnotation = Annotations
                .OfType<ConnectionStringRedirectAnnotation>()
                .FirstOrDefault() ?? throw new InvalidOperationException("Mongo replica set must have connection string redirection.");

            var builder = new ReferenceExpressionBuilder();

            builder.AppendFormatted(connectionStringAnnotation.Resource.ConnectionStringExpression);
            builder.Append($"&directConnection=true");

            return builder.Build();
        }
    }

    public IResourceWithConnectionString Parent { get; } = parent;
}