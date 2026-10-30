using Godot;
using WarmachineClient.Autoload;

namespace WarmachineClient.Scenes.Main;

public partial class Settings : Control
{
    private LineEdit _baseUrlInput = null!;
    private LineEdit _apiKeyInput = null!;
    private Button _saveButton = null!;
    private Button _backButton = null!;
    private Label _statusLabel = null!;
    private ApiSettings _apiSettings = null!;

    public override void _Ready()
    {
        _baseUrlInput = GetNode<LineEdit>("VBoxContainer/BaseUrlInput");
        _apiKeyInput = GetNode<LineEdit>("VBoxContainer/ApiKeyInput");
        _saveButton = GetNode<Button>("VBoxContainer/ButtonRow/SaveButton");
        _backButton = GetNode<Button>("VBoxContainer/ButtonRow/BackButton");
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");
        _apiSettings = GetNode<ApiSettings>("/root/ApiSettings");

        _baseUrlInput.Text = _apiSettings.BaseUrl;
        _apiKeyInput.Text = _apiSettings.ApiKey;

        _saveButton.Pressed += OnSavePressed;
        _backButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Main/MainMenu.tscn");
    }

    private void OnSavePressed()
    {
        _apiSettings.Save(_baseUrlInput.Text, _apiKeyInput.Text);
        _statusLabel.Text = "Saved.";
    }
}
