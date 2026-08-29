namespace EarlyEagles.Tests.Integration.Persistence;

/// <summary>
/// Shares a single <see cref="PostgresFixture"/> (one container) across every test
/// class tagged with <c>[Collection(PostgresCollectionDefinition.Name)]</c>, so
/// container startup + migration happen once for the whole collection.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
