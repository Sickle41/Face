using System;

namespace WarmachineClient.Api.Dto;

public class DeploymentZone
{
    public Guid Id { get; set; }
    public Guid MapId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}
