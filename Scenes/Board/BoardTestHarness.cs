using System;
using System.Threading.Tasks;
using Godot;
using WarmachineClient.Autoload;

namespace WarmachineClient.Scenes.Board;

public partial class BoardTestHarness : CanvasLayer
{
    private LineEdit _mapIdInput = null!;
    private Button _loadButton = null!;
    private Button _mainMenuButton = null!;
    private Label _statusLabel = null!;
    private Board _board = null!;

    public override void _Ready()
    {
        _mapIdInput = GetNode<LineEdit>("Panel/VBoxContainer/MapIdInput");
        _loadButton = GetNode<Button>("Panel/VBoxContainer/LoadButton");
        _mainMenuButton = GetNode<Button>("Panel/VBoxContainer/MainMenuButton");
        _statusLabel = GetNode<Label>("Panel/VBoxContainer/StatusLabel");
        _board = GetParent<Board>();

        _loadButton.Pressed += OnLoadButtonPressed;
        _mainMenuButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Main/MainMenu.tscn");

        var catalogState = GetNode<CatalogState>("/root/CatalogState");
        if (catalogState.SelectedMapId.HasValue)
        {
            var mapId = catalogState.SelectedMapId.Value;
            _mapIdInput.Text = mapId.ToString();
            _ = LoadMap(mapId);
        }
    }

    private async void OnLoadButtonPressed()
    {
        if (!Guid.TryParse(_mapIdInput.Text, out var mapId))
        {
            _statusLabel.Text = "Invalid map id.";
            return;
        }

        await LoadMap(mapId);
    }

    private async Task LoadMap(Guid mapId)
    {
        _statusLabel.Text = "Loading...";
        _loadButton.Disabled = true;

        try
        {
            await _board.LoadMap(mapId);
            _statusLabel.Text = "Loaded.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
        finally
        {
            _loadButton.Disabled = false;
        }
    }
}
