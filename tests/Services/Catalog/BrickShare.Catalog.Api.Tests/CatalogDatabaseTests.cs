using System;
using System.Threading.Tasks;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

public abstract class CatalogDatabaseTests(CatalogApiFactory factory) : IAsyncLifetime
{
    protected CatalogApiFactory Factory { get; } = factory;

    public async ValueTask InitializeAsync() => await Factory.ResetDatabaseAsync();

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }
}