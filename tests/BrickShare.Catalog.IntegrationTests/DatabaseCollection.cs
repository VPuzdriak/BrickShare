namespace BrickShare.Catalog.IntegrationTests;

/// <summary>
/// Every test that touches Postgres joins this collection. It shares one container — and,
/// just as importantly, it stops the classes running at the same time as each other.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<CatalogDatabase>
{
    public const string Name = "catalog database";
}
