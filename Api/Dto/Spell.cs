using System;

namespace WarmachineClient.Api.Dto;

public class Spell
{
    public Guid Id { get; set; }
    public Guid UnitDefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Cost { get; set; }
    public int Range { get; set; }
    public int Aoe { get; set; }
    public int Pow { get; set; }
    public bool IsUpkeep { get; set; }
    public bool IsOffensive { get; set; }
    public string Description { get; set; } = string.Empty;
}
