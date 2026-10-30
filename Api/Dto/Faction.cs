using System;

namespace WarmachineClient.Api.Dto;

public class Faction
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
