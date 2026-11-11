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

        DrawLine(_startPoint, _endPoint, Colors.Yellow, 2f);

        var distanceMm = _startPoint.DistanceTo(_endPoint) / Board.PixelsPerUnit;
        var distanceInches = Units.ToInches(distanceMm);
        var midpoint = (_startPoint + _endPoint) / 2f;

        DrawString(
            ThemeDB.GetFallbackFont(),
            midpoint + new Vector2(6, -6),
            $"{distanceInches:0.0}\"",
            HorizontalAlignment.Left,
            -1,
            16,
            Colors.Yellow);
    }
}
