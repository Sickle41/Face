using System;

namespace WarmachineClient.Api.Dto;

public class ControlZone
{
    public Guid Id { get; set; }
    public Guid ScenarioId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Radius { get; set; }
    public int PointsPerTurn { get; set; }
}
