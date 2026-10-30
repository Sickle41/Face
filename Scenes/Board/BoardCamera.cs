using Godot;

namespace WarmachineClient.Scenes.Board;

public partial class BoardCamera : Camera2D
{
    private const float ZoomStep = 0.1f;
    private const float MinZoom = 0.25f;
    private const float MaxZoom = 4f;

    private bool _dragging;

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Middle } mouseButton:
                _dragging = mouseButton.Pressed;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }:
                ApplyZoom(ZoomStep);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
                ApplyZoom(-ZoomStep);
                break;
            case InputEventMouseMotion mouseMotion when _dragging:
                Position -= mouseMotion.Relative / Zoom;
                break;
        }
    }

    private void ApplyZoom(float delta)
    {
        var newZoom = Mathf.Clamp(Zoom.X + delta, MinZoom, MaxZoom);
        Zoom = new Vector2(newZoom, newZoom);
    }
}
