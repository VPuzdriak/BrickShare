using System;

using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.Api.Tests;

public static class SetSpecificationData
{
    public static SetSpecification Titanic() => new(
        RebrickableSetNumber.Parse("10294-1").Value,
        "Titanic",
        2021,
        9092,
        3,
        new RebrickableTheme(1008, "Sub Theme", new RebrickableTheme(158, "Top Theme", null)),
        new Uri("https://cdn.rebrickable.com/media/sets/10294-1.jpg"),
        new Uri("https://rebrickable.com/sets/10294-1/titanic/"));
}