using System;

namespace WarmachineClient.Api.Dto;

public class TerrainFeature
{
    public Guid Id { get; set; }
    public Guid MapId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TerrainType TerrainType { get; set; }
    public TerrainShape Shape { get; set; }

    // Center point for both shapes.
    public double X { get; set; }
    public double Y { get; set; }

    // Used when Shape == Rectangle.
    public double Width { get; set; }
    public double Height { get; set; }

    // Used when Shape == Circle.
    public double Radius { get; set; }

    public bool BlocksLineOfSight { get; set; }
    public bool GrantsConcealment { get; set; }
    public bool GrantsCover { get; set; }
    public MovementEffect MovementEffect { get; set; } = MovementEffect.Normal;
}
