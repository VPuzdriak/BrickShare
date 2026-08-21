using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.UnitTests;

public class CopyStatusTests
{
    [Fact]
    public void A_newly_registered_copy_is_available()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New);

        Assert.Equal(CopyStatus.Available, copy.Status);
    }

    [Fact]
    public void An_available_copy_can_be_reserved()
    {
        Copy copy = Available();

        copy.Reserve();

        Assert.Equal(CopyStatus.Reserved, copy.Status);
    }

    [Fact]
    public void A_reserved_copy_can_be_collected()
    {
        Copy copy = Available();
        copy.Reserve();

        copy.Collect();

        Assert.Equal(CopyStatus.OnRent, copy.Status);
    }

    [Fact]
    public void A_returned_copy_is_awaiting_inspection()
    {
        Copy copy = OnRent();

        copy.Return();

        Assert.Equal(CopyStatus.AwaitingInspection, copy.Status);
    }

    [Fact]
    public void A_returned_copy_can_be_taken_in_for_inspection()
    {
        Copy copy = OnRent();
        copy.Return();

        copy.BeginInspection();

        Assert.Equal(CopyStatus.InInspection, copy.Status);
    }

    [Fact]
    public void An_inspected_copy_can_be_shelved()
    {
        Copy copy = InInspection();

        copy.Shelve();

        Assert.Equal(CopyStatus.Available, copy.Status);
    }

    [Fact]
    public void A_copy_that_nobody_reserved_cannot_be_collected()
    {
        Copy copy = Available();

        Assert.Throws<InvalidOperationException>(copy.Collect);
    }

    [Fact]
    public void A_copy_that_is_out_cannot_be_reserved()
    {
        Copy copy = OnRent();

        Assert.Throws<InvalidOperationException>(() => copy.Reserve());
    }

    [Fact]
    public void An_unclaimed_reservation_returns_the_copy_to_the_shelf()
    {
        Copy copy = Available();
        copy.Reserve();

        copy.ReleaseReservation();

        Assert.Equal(CopyStatus.Available, copy.Status);
    }

    [Fact]
    public void An_inspected_copy_can_be_sent_for_repair()
    {
        Copy copy = InInspection();

        copy.SendForRepair();

        Assert.Equal(CopyStatus.InRepair, copy.Status);
    }

    [Fact]
    public void A_repaired_copy_returns_to_the_shelf()
    {
        Copy copy = InRepair();

        copy.CompleteRepair();

        Assert.Equal(CopyStatus.Available, copy.Status);
    }

    [Fact]
    public void A_copy_that_is_not_in_repair_cannot_complete_a_repair()
    {
        Copy copy = InInspection();

        Assert.Throws<InvalidOperationException>(() => copy.CompleteRepair());
    }

    [Fact]
    public void A_copy_on_the_shelf_can_be_retired()
    {
        Copy copy = Available();

        copy.Retire();

        Assert.Equal(CopyStatus.Retired, copy.Status);
    }

    [Fact]
    public void A_copy_that_is_out_on_rent_cannot_be_retired()
    {
        Copy copy = OnRent();

        Assert.Throws<InvalidOperationException>(() => copy.Retire());
    }

    [Fact]
    public void A_copy_being_inspected_can_be_retired()
    {
        Copy copy = InInspection();

        copy.Retire();

        Assert.Equal(CopyStatus.Retired, copy.Status);
    }

    [Fact]
    public void A_copy_that_was_never_returned_is_written_off_as_lost()
    {
        Copy copy = OnRent();

        copy.WriteOffAsLost();

        Assert.Equal(CopyStatus.Lost, copy.Status);
    }

    [Fact]
    public void A_copy_on_the_shelf_cannot_be_written_off_as_lost()
    {
        Copy copy = Available();

        Assert.Throws<InvalidOperationException>(copy.WriteOffAsLost);
    }

    [Fact]
    public void A_retired_copy_cannot_be_reserved()
    {
        Copy copy = Available();
        copy.Retire();

        Assert.Throws<InvalidOperationException>(copy.Reserve);
    }

    [Fact]
    public void A_recovered_copy_re_enters_stock_awaiting_inspection()
    {
        Copy copy = OnRent();
        copy.WriteOffAsLost();

        copy.Recover();

        Assert.Equal(CopyStatus.AwaitingInspection, copy.Status);
    }

    [Fact]
    public void A_retired_copy_cannot_be_recovered()
    {
        Copy copy = Available();
        copy.Retire();

        Assert.Throws<InvalidOperationException>(copy.Recover);
    }

    private static Copy Available() =>
        Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New);

    private static Copy OnRent()
    {
        var copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New);
        copy.Reserve();
        copy.Collect();

        return copy;
    }

    private static Copy InInspection()
    {
        var copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New);
        copy.Reserve();
        copy.Collect();
        copy.Return();
        copy.BeginInspection();

        return copy;
    }

    private static Copy InRepair()
    {
        var copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New);
        copy.Reserve();
        copy.Collect();
        copy.Return();
        copy.BeginInspection();
        copy.SendForRepair();

        return copy;
    }
}
