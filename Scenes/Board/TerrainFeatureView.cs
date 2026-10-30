using Godot;
using WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Board;

public partial class TerrainFeatureView : Node2D
{
    private TerrainFeature? _feature;

    public void SetFeature(TerrainFeature feature)
    {
        _feature = feature;
        Position = new Vector2((float)feature.X, (float)feature.Y) * Board.PixelsPerUnit;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_feature is null)
        {
            return;
        }

        var fillColor = TerrainColor(_feature.TerrainType);
        fillColor.A = 0.45f;

        if (_feature.Shape == TerrainShape.Circle)
        {
            var radius = (float)_feature.Radius * Board.PixelsPerUnit;
            DrawCircle(Vector2.Zero, radius, fillColor);
            if (_feature.BlocksLineOfSight)
            {
                DrawArc(Vector2.Zero, radius, 0, Mathf.Tau, 48, Colors.Black, 2f);
            }
        }
        else
        {
            var halfWidth = (float)_feature.Width * Board.PixelsPerUnit / 2f;
            var halfHeight = (float)_feature.Height * Board.PixelsPerUnit / 2f;
            var rect = new Rect2(-halfWidth, -halfHeight, halfWidth * 2f, halfHeight * 2f);
            DrawRect(rect, fillColor);
            if (_feature.BlocksLineOfSight)
            {
                DrawRect(rect, Colors.Black, filled: false, width: 2f);
            }
        }
    }

    private static Color TerrainColor(TerrainType type) => type switch
    {
        TerrainType.Forest => new Color(0.13f, 0.4f, 0.13f),
        TerrainType.Hill => new Color(0.55f, 0.45f, 0.25f),
        TerrainType.Water => new Color(0.2f, 0.4f, 0.8f),
        TerrainType.Obstacle => new Color(0.5f, 0.5f, 0.5f),
        TerrainType.Obstruction => new Color(0.6f, 0.6f, 0.65f),
        TerrainType.Rubble => new Color(0.45f, 0.4f, 0.35f),
        TerrainType.DustDevil => new Color(0.8f, 0.7f, 0.4f),
        TerrainType.DenseFog => new Color(0.8f, 0.8f, 0.85f),
        TerrainType.Crater => new Color(0.35f, 0.3f, 0.25f),
        TerrainType.Storm => new Color(0.3f, 0.3f, 0.5f),
        TerrainType.TallGrass => new Color(0.5f, 0.7f, 0.3f),
        TerrainType.Trench => new Color(0.4f, 0.35f, 0.3f),
        TerrainType.Hazard => new Color(0.8f, 0.2f, 0.2f),
        _ => Colors.Gray,
    };
}
