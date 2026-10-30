using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using WarmachineClient.Autoload;
using WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Main;

public partial class MapPicker : Control
{
    private ItemList _list = null!;
    private Button _refreshButton = null!;
    private Button _backButton = null!;
    private Label _statusLabel = null!;
    private ApiSettings _apiSettings = null!;
    private CatalogState _catalogState = null!;
    private List<Map> _maps = new();

    public override void _Ready()
    {
        _list = GetNode<ItemList>("VBoxContainer/List");
        _refreshButton = GetNode<Button>("VBoxContainer/TopRow/RefreshButton");
        _backButton = GetNode<Button>("VBoxContainer/TopRow/BackButton");
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");
        _apiSettings = GetNode<ApiSettings>("/root/ApiSettings");
        _catalogState = GetNode<CatalogState>("/root/CatalogState");

        _refreshButton.Pressed += () => _ = LoadMaps();
        _backButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Main/MainMenu.tscn");
        _list.ItemActivated += OnMapActivated;

        _ = LoadMaps();
    }

    private async Task LoadMaps()
    {
        _statusLabel.Text = "Loading...";
        try
        {
            _maps = await _apiSettings.Client.GetAsync<List<Map>>("/api/maps");
            _list.Clear();
            foreach (var map in _maps)
            {
                _list.AddItem($"{map.Name} ({map.Width:0}x{map.Height:0})");
            }

            _statusLabel.Text = _maps.Count == 0 ? "No maps found." : string.Empty;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private void OnMapActivated(long index)
    {
        var map = _maps[(int)index];
        _catalogState.SelectedMapId = map.Id;
        GetTree().ChangeSceneToFile("res://Scenes/Board/Board.tscn");
    }
}
