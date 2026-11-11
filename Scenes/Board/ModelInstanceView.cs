using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using WarmachineClient.Autoload;
using WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Board;

public partial class ModelInstanceView : Node2D
{
    [Signal]
    public delegate void SelectedEventHandler(ModelInstanceView view);

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
    private bool _selected;
    private bool _dragging;

    public Guid ModelId => _model?.Id ?? Guid.Empty;

    public override void _Ready()
    {
        _label = GetNode<Label>("Label");
        _hoverArea = GetNode<Area2D>("HoverArea");
        _hoverCircle = (CircleShape2D)GetNode<CollisionShape2D>("HoverArea/CollisionShape").Shape;

        _label.Visible = false;
        _hoverArea.MouseEntered += () => _label.Visible = true;
        _hoverArea.MouseExited += () => _label.Visible = false;
        _hoverArea.InputEvent += OnHoverAreaInputEvent;
    }

    public void SetModel(ModelInstance model, UnitDefinition unit, Color color, string designation)
    {
        _model = model;
        _color = color;
        _radius = RadiusByBaseSize.TryGetValue(unit.BaseSize, out var radius) ? radius : 20f;

        Position = new Vector2((float)model.X, (float)model.Y) * Board.PixelsPerUnit;

        var pixelRadius = _radius * Board.PixelsPerUnit;
        _hoverCircle.Radius = pixelRadius;
        _label.Text = $"{unit.Name} {designation}";
        _label.Position = new Vector2(-50f, pixelRadius + 2f);
        QueueRedraw();
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_model is null)
        {
            return;
        }

        var pixelRadius = _radius * Board.PixelsPerUnit;
        var fillColor = _model.HasActivated ? _color.Lerp(Colors.Gray, 0.6f) : _color;
        DrawCircle(Vector2.Zero, pixelRadius, fillColor);
        DrawArc(Vector2.Zero, pixelRadius, 0, Mathf.Tau, 32, Colors.Black, 1.5f);

        if (_selected)
        {
            DrawArc(Vector2.Zero, pixelRadius + 4f, 0, Mathf.Tau, 32, Colors.Yellow, 3f);
        }
    }

    private void OnHoverAreaInputEvent(Node viewport, InputEvent @event, long shapeIdx)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            EmitSignal(SignalName.Selected, this);
            _dragging = true;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_dragging)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseMotion:
                GlobalPosition = GetGlobalMousePosition();
                QueueRedraw();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
                _dragging = false;
                _ = CommitPosition();
                break;
        }
    }

    private async Task CommitPosition()
    {
        if (_model is null)
        {
            return;
        }

        var worldPos = GlobalPosition / Board.PixelsPerUnit;
        var apiSettings = GetNode<ApiSettings>("/root/ApiSettings");

        try
        {
            await apiSettings.Client.PatchAsync($"/api/models/{_model.Id}/position", new
            {
                x = worldPos.X,
                y = worldPos.Y,
                facing = _model.Facing
            });
            _model.X = worldPos.X;
            _model.Y = worldPos.Y;
        }
        catch (Exception)
        {
            Position = new Vector2((float)_model.X, (float)_model.Y) * Board.PixelsPerUnit;
            QueueRedraw();
        }
    }
}
