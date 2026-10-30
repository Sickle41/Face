using System;

namespace WarmachineClient.Api.Dto;

public class UnitAttachment
{
    public Guid Id { get; set; }
    public Guid UnitDefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PointCost { get; set; }
    public string Description { get; set; } = string.Empty;
}
