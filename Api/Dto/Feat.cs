using System;

namespace WarmachineClient.Api.Dto;

public class Feat
{
    public Guid Id { get; set; }
    public Guid UnitDefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
