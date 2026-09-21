using BrickShare.Catalog.Api.Persistence;
using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace BrickShare.Catalog.IntegrationTests.Persistence;

public class CopyPersistenceTests(CatalogDatabase database) : DatabaseTest(database)
{
    [Fact]
    public async Task A_registered_copy_comes_back_as_the_copy_that_was_registered()
    {
        Guid setId = await ACataloguedSetAsync();
        Copy registered = Copy.Register(setId, LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.Good, 9200);

        await using (CatalogDbContext writing = Database.NewDbContext())
        {
            writing.Copies.Add(registered);
            await writing.SaveChangesAsync();
        }

        await using CatalogDbContext reading = Database.NewDbContext();
        Copy read = await reading.Copies.SingleAsync();

        Assert.Equal(setId, read.CatalogSetId);
        Assert.Equal(registered.Id, read.Id);
        Assert.Equal(LabelCode.Parse("BRK-7F3K2Q"), read.Label);
        Assert.Equal(ConditionGrade.Good, read.Grade);
        Assert.Equal(9200, read.BaselineWeightInGrams);
        Assert.Equal(CopyStatus.Available, read.Status);
        Assert.Null(read.RetiredAt);
    }

    [Fact]
    public async Task Two_copies_cannot_carry_the_same_label_code()
    {
        Guid setId = await ACataloguedSetAsync();
        LabelCode label = LabelCode.Parse("BRK-7F3K2Q");

        await using CatalogDbContext context = Database.NewDbContext();
        context.Copies.Add(Copy.Register(setId, label, ConditionGrade.New, 9200));
        context.Copies.Add(Copy.Register(setId, label, ConditionGrade.Good, 9200));

        DbUpdateException error =
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        PostgresException postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("ix_copies_label_code", postgres.ConstraintName);
    }

    [Fact]
    public async Task The_second_of_two_people_writing_to_the_same_copy_is_refused()
    {
        Guid setId = await ACataloguedSetAsync();
        Copy copy = Copy.Register(setId, LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.Good, 9200);
        copy.Reserve();
        copy.Collect();
        copy.Return();
        copy.BeginInspection();

        await using (CatalogDbContext seeding = Database.NewDbContext())
        {
            seeding.Copies.Add(copy);
            await seeding.SaveChangesAsync();
        }

        // Two staff members, two screens, one box on the bench between them.
        await using CatalogDbContext inspector = Database.NewDbContext();
        await using CatalogDbContext colleague = Database.NewDbContext();

        Copy asInspectorSeesIt = await inspector.Copies.SingleAsync();
        Copy asColleagueSeesIt = await colleague.Copies.SingleAsync();

        asInspectorSeesIt.SendForRepair();
        await inspector.SaveChangesAsync();

        asColleagueSeesIt.Shelve();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => colleague.SaveChangesAsync());
    }

    private async Task<Guid> ACataloguedSetAsync()
    {
        CatalogSet set = CatalogSet.Catalogue(
            SetNumber.Parse("10294-1"), "Titanic", "Icons", 2021, 9092,
            new Money(629.99m), new Money(60.00m), 7, 18);

        await using CatalogDbContext context = Database.NewDbContext();
        context.Sets.Add(set);
        await context.SaveChangesAsync();

        return set.Id;
    }
}
