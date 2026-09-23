using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.IntegrationTests.Copies;

/// <summary>
/// Hands out the labels it was given, in order, and repeats the last one once it runs out.
/// The whole point of the seam: a mint whose next answer the test already knows.
/// </summary>
internal sealed class ScriptedLabelCodeMinter(params string[] labels) : ILabelCodeMinter
{
    private int _issued;

    public LabelCode Mint()
    {
        string label = labels[Math.Min(_issued, labels.Length - 1)];
        _issued++;

        return LabelCode.Parse(label);
    }
}
