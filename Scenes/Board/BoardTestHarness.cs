using System;
using System.Threading.Tasks;
using Godot;
using WarmachineClient.Autoload;

namespace WarmachineClient.Scenes.Board;

public partial class BoardTestHarness : CanvasLayer
{
    private LineEdit _mapIdInput = null!;
    private Button _loadButton = null!;
    private Button _spawnModelsButton = null!;
    private Button _measureButton = null!;
    private Button _mainMenuButton = null!;
    private Label _statusLabel = null!;
    private Board _board = null!;
    private MeasuringToolOverlay _measuringTool = null!;

    public override void _Ready()
    {
        _mapIdInput = GetNode<LineEdit>("Panel/VBoxContainer/MapIdInput");
        _loadButton = GetNode<Button>("Panel/VBoxContainer/LoadButton");
        _spawnModelsButton = GetNode<Button>("Panel/VBoxContainer/SpawnModelsButton");
        _measureButton = GetNode<Button>("Panel/VBoxContainer/MeasureButton");
        _mainMenuButton = GetNode<Button>("Panel/VBoxContainer/MainMenuButton");
        _statusLabel = GetNode<Label>("Panel/VBoxContainer/StatusLabel");
        _board = GetParent<Board>();
        _measuringTool = GetNode<MeasuringToolOverlay>("../MeasuringTool");

        _loadButton.Pressed += OnLoadButtonPressed;
        _spawnModelsButton.Pressed += () => _ = SpawnModels();
        _measureButton.Pressed += () => _measuringTool.SetEnabled(_measureButton.ButtonPressed);
        _mainMenuButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Main/MainMenu.tscn");

        CallDeferred(nameof(AutoLoadFromCatalogState));
    }

    private void AutoLoadFromCatalogState()
    {
        var catalogState = GetNode<CatalogState>("/root/CatalogState");
        if (catalogState.SelectedSessionId.HasValue)
        {
            _ = LoadSession(catalogState.SelectedSessionId.Value);
        }
        else if (catalogState.SelectedMapId.HasValue)
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

    private async Task LoadSession(Guid sessionId)
    {
        _statusLabel.Text = "Loading session...";
        try
        {
            await _board.LoadSession(sessionId);
            _mapIdInput.Text = string.Empty;
            _spawnModelsButton.Visible = true;
            _statusLabel.Text = "Session loaded.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private async Task SpawnModels()
    {
        _statusLabel.Text = "Spawning...";
        _spawnModelsButton.Disabled = true;

        try
        {
            await _board.SpawnMissingModels();
            _statusLabel.Text = "Models spawned.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
        finally
        {
            _spawnModelsButton.Disabled = false;
        }
    }
}
