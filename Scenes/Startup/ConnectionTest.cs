using System;
using Godot;
using WarmachineClient.Autoload;

namespace WarmachineClient.Scenes.Startup;

public partial class ConnectionTest : Control
{
    private Label _statusLabel = null!;
    private Button _testButton = null!;
    private ApiSettings _apiSettings = null!;

    public override void _Ready()
    {
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");
        _testButton = GetNode<Button>("VBoxContainer/TestButton");
        _apiSettings = GetNode<ApiSettings>("/root/ApiSettings");

        _testButton.Pressed += OnTestButtonPressed;
    }

    private async void OnTestButtonPressed()
    {
        _statusLabel.Text = "Checking...";
        _testButton.Disabled = true;

        try
        {
            var healthy = await _apiSettings.Client.CheckHealthAsync();
            _statusLabel.Text = healthy
                ? $"Reachable: {_apiSettings.BaseUrl}"
                : "Unreachable (unhealthy response)";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Unreachable: {ex.Message}";
        }
        finally
        {
            _testButton.Disabled = false;
        }
    }
}
