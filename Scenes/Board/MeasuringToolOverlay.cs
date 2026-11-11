using Godot;
using WarmachineClient.Rules;

namespace WarmachineClient.Scenes.Board;

public partial class MeasuringToolOverlay : Node2D
{
    private bool _enabled;
    private bool _dragging;
    private bool _hasMeasurement;
    private Vector2 _startPoint;
    private Vector2 _endPoint;
    private Board _board = null!;

    public override void _Ready()
    {
        _board = GetParent<Board>();
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        if (!enabled)
        {
            _dragging = false;
            _hasMeasurement = false;
        }

        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_enabled)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }:
                _dragging = true;
                _hasMeasurement = true;
                _startPoint = ToLocal(GetGlobalMousePosition());
                _endPoint = _startPoint;
                QueueRedraw();
                break;
            case InputEventMouseMotion when _dragging:
                _endPoint = ToLocal(GetGlobalMousePosition());
                QueueRedraw();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } when _dragging:
                _dragging = false;
                _endPoint = ToLocal(GetGlobalMousePosition());
                QueueRedraw();
                break;
        }
    }

    public override void _Draw()
    {
        if (!_enabled || !_hasMeasurement)
        {
            return;
        }

        var startWorld = _startPoint / Board.PixelsPerUnit;
        var endWorld = _endPoint / Board.PixelsPerUnit;
        var clear = LineOfSight.HasClearLineOfSight(startWorld, endWorld, _board.Terrain);
        var color = clear ? Colors.LightGreen : Colors.OrangeRed;

        DrawLine(_startPoint, _endPoint, color, 2f);

        var distanceMm = _startPoint.DistanceTo(_endPoint) / Board.PixelsPerUnit;
        var distanceInches = Units.ToInches(distanceMm);
        var midpoint = (_startPoint + _endPoint) / 2f;
        var label = clear ? $"{distanceInches:0.0}\"" : $"{distanceInches:0.0}\" (blocked)";

        DrawString(
            ThemeDB.GetFallbackFont(),
            midpoint + new Vector2(6, -6),
            label,
            HorizontalAlignment.Left,
            -1,
            16,
            color);
    }
}
