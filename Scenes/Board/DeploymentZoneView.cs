using Godot;
using WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Board;

public partial class DeploymentZoneView : Node2D
{
    private DeploymentZone? _zone;
    private Label _label = null!;

    public override void _Ready()
    {
        _label = GetNode<Label>("Label");
    }

    public void SetZone(DeploymentZone zone)
    {
        _zone = zone;
        Position = new Vector2((float)zone.X, (float)zone.Y) * Board.PixelsPerUnit;
        _label.Text = zone.Name;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_zone is null)
        {
            return;
        }

        var width = (float)_zone.Width * Board.PixelsPerUnit;
        var height = (float)_zone.Height * Board.PixelsPerUnit;
        var rect = new Rect2(0, 0, width, height);
        DrawRect(rect, new Color(0.9f, 0.9f, 0.2f, 0.25f));
        DrawRect(rect, new Color(0.9f, 0.9f, 0.2f, 0.8f), filled: false, width: 2f);
    }
}
