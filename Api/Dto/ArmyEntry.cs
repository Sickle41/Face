using System;

namespace WarmachineClient.Api.Dto;

public class ArmyEntry
{
    public Guid Id { get; set; }
    public Guid ArmyId { get; set; }
    public Guid UnitDefinitionId { get; set; }
    public int Quantity { get; set; } = 1;
    public Guid? UnitAttachmentId { get; set; }
    public Guid? BattlegroupCasterEntryId { get; set; }
}
