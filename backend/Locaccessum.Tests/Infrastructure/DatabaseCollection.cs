namespace Locaccessum.Tests.Infrastructure;

[CollectionDefinition("db")]
public class DatabaseCollection : ICollectionFixture<PostgresFixture>;
