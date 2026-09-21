using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.UnitTests;

internal static class ACopy
{
    public const int Grams = 9200;

    public static Copy Graded(ConditionGrade grade, LabelCode? labelCode = null) =>
        Copy.Register(Guid.CreateVersion7(), labelCode ?? LabelCode.Mint(), grade, Grams);
}
