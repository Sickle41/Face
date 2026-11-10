using System.Collections.Generic;
using Godot;
using WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Board;

public partial class ModelInstanceView : Node2D
{
    private static readonly Dictionary<BaseSize, float> RadiusByBaseSize = new()
    {
        [BaseSize.Small] = 15f,
        [BaseSize.Medium] = 20f,
        [BaseSize.Large] = 25f,
        [BaseSize.ExtraLarge] = 45f,
        [BaseSize.Huge] = 60f
    };

    private Label _label = null!;
    private Area2D _hoverArea = null!;
    private CircleShape2D _hoverCircle = null!;
    private ModelInstance? _model;
    private float _radius = 20f;
    private Color _color = Colors.White;

    public System.Guid ModelId => _model?.Id ?? System.Guid.Empty;

    public override void _Ready()
    {
        _label = GetNode<Label>("Label");
        _hoverArea = GetNode<Area2D>("HoverArea");
        _hoverCircle = (CircleShape2D)GetNode<CollisionShape2D>("HoverArea/CollisionShape").Shape;

        _label.Visible = false;
        _hoverArea.MouseEntered += () => _label.Visible = true;
        _hoverArea.MouseExited += () => _label.Visible = false;
    }

    public void SetModel(ModelInstance model, UnitDefinition unit, Color color)
    {
        _model = model;
        _color = color;
        _radius = RadiusByBaseSize.TryGetValue(unit.BaseSize, out var radius) ? radius : 20f;

        Position = new Vector2((float)model.X, (float)model.Y) * Board.PixelsPerUnit;

        var pixelRadius = _radius * Board.PixelsPerUnit;
        _hoverCircle.Radius = pixelRadius;
        _label.Text = unit.Name;
        _label.Position = new Vector2(-50f, pixelRadius + 2f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_model is null)
        {
            return;
        }

        var pixelRadius = _radius * Board.PixelsPerUnit;
        DrawCircle(Vector2.Zero, pixelRadius, _color);
        DrawArc(Vector2.Zero, pixelRadius, 0, Mathf.Tau, 32, Colors.Black, 1.5f);
    }
}
