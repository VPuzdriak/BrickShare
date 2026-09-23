namespace BrickShare.Catalog.Domain;

public interface ILabelCodeMinter
{
    LabelCode Mint();
}

public sealed class RandomLabelCodeMinter : ILabelCodeMinter
{
    public LabelCode Mint() => LabelCode.Mint();
}
