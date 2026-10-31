using System.Collections.Generic;

namespace WarmachineClient.Api.Dto;

public class Battlegroup
{
    public ArmyEntry? Caster { get; set; }
    public List<ArmyEntry> Members { get; set; } = new();
}
