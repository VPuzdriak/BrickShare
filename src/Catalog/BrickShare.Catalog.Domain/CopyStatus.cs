namespace BrickShare.Catalog.Domain;

public enum CopyStatus
{
    Available,
    Reserved,
    OnRent,
    AwaitingInspection,
    InInspection,
    InRepair,
    Lost,
    Retired
}
