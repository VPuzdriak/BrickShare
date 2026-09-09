namespace BrickShare.Catalog.Domain;

/// <summary>
/// One LEGO set the shop catalogues. The shop may own three Titanics: this is the single
/// description they share, and <see cref="Copy"/> is one of the three boxes.
/// </summary>
public sealed class CatalogSet
{
    /// <summary>
    /// No rental may be longer than this. UC-2's write-off fires on day 28, while the deposit
    /// authorization — which dies at 30 — is still capturable. A minimum longer than the maximum
    /// would be a set the shop sells and cannot recover.
    /// </summary>
    public const int MaximumRentalDays = 28;

    private CatalogSet(
        SetNumber number,
        string name,
        string theme,
        int year,
        int pieceCount,
        Money retailPrice,
        Money baseRentalPrice,
        int minimumRentalDays,
        int minimumAge)
    {
        Id = Guid.CreateVersion7();
        Number = number;
        Name = name;
        Theme = theme;
        Year = year;
        PieceCount = pieceCount;
        RetailPrice = retailPrice;
        BaseRentalPrice = baseRentalPrice;
        MinimumRentalDays = minimumRentalDays;
        MinimumAge = minimumAge;
    }

    public Guid Id { get; }

    public SetNumber Number { get; }

    public string Name { get; }

    public string Theme { get; }

    public int Year { get; }

    public int PieceCount { get; }

    public Money RetailPrice { get; }

    public Money BaseRentalPrice { get; }

    public int MinimumRentalDays { get; }

    public int MinimumAge { get; }

    public static CatalogSet Catalogue(
        SetNumber number,
        string name,
        string theme,
        int year,
        int pieceCount,
        Money retailPrice,
        Money baseRentalPrice,
        int minimumRentalDays,
        int minimumAge)
    {
        ArgumentNullException.ThrowIfNull(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(theme);
        ArgumentOutOfRangeException.ThrowIfLessThan(pieceCount, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(retailPrice.Amount);
        ArgumentOutOfRangeException.ThrowIfNegative(baseRentalPrice.Amount);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumAge);
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumRentalDays, 1);

        if (minimumRentalDays > MaximumRentalDays)
        {
            throw new InvalidOperationException(
                $"A set cannot require {minimumRentalDays} days. The shop rents for at most "
                + $"{MaximumRentalDays} days, so a longer minimum could never be met.");
        }

        return new CatalogSet(
            number, name, theme, year, pieceCount,
            retailPrice, baseRentalPrice, minimumRentalDays, minimumAge);
    }
}
