using Xunit;

namespace BrickShare.Catalog.Api.Tests;

[CollectionDefinition(CatalogCollectionDefinition.Name)]
public sealed class CatalogCollectionDefinition : ICollectionFixture<CatalogApiFactory>
{
    public const string Name = "Catalog";
}