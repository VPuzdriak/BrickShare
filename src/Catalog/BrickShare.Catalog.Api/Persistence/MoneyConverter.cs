using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BrickShare.Catalog.Api.Persistence;

public sealed class MoneyConverter() : ValueConverter<Money, decimal>(
    money => money.Amount,
    amount => new Money(amount));
