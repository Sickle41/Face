using System.Collections.Generic;

namespace WarmachineClient.Api.Dto;

public class ArmyExportEntry
{
    public string UnitName { get; set; } = string.Empty;
    public UnitCategory Category { get; set; }
    public int PointCost { get; set; }
    public int Quantity { get; set; }
    public string? AttachmentName { get; set; }
    public string? BattlegroupCasterName { get; set; }
}

public class ArmyExport
{
    public string ArmyName { get; set; } = string.Empty;
    public string FactionName { get; set; } = string.Empty;
    public int PointLimit { get; set; }
    public int TotalPoints { get; set; }
    public List<ArmyExportEntry> Entries { get; set; } = new();
}
