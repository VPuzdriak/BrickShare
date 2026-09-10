using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.UnitTests;

public class CatalogSetTests
{
    [Fact]
    public void A_set_cannot_require_a_rental_longer_than_the_shop_can_recover_it_in()
    {
        Assert.Throws<DomainRuleViolationException>(() => Catalogue(minimumRentalDays: 29));
    }

    [Fact]
    public void A_catalogued_set_keeps_the_facts_it_was_given()
    {
        CatalogSet set = Catalogue();

        Assert.Equal(SetNumber.Parse("10294-1"), set.Number);
        Assert.Equal("Titanic", set.Name);
        Assert.Equal(new Money(629.99m), set.RetailPrice);
        Assert.Equal(7, set.MinimumRentalDays);
        Assert.NotEqual(Guid.Empty, set.Id);
    }

    [Fact]
    public void A_set_may_be_catalogued_at_exactly_the_maximum_rental_period()
    {
        CatalogSet set = Catalogue(minimumRentalDays: CatalogSet.MaximumRentalDays);

        Assert.Equal(28, set.MinimumRentalDays);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_set_must_be_rentable_for_at_least_one_day(int days)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Catalogue(minimumRentalDays: days));
    }

    [Fact]
    public void A_set_cannot_be_catalogued_at_a_negative_retail_price()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Catalogue(retailPrice: new Money(-1m)));
    }

    private static CatalogSet Catalogue(
        int minimumRentalDays = 7,
        Money? retailPrice = null) =>
        CatalogSet.Catalogue(
            SetNumber.Parse("10294-1"),
            "Titanic",
            "Icons",
            2021,
            9090,
            retailPrice ?? new Money(629.99m),
            new Money(60.00m),
            minimumRentalDays,
            18);
}
