using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using WarmachineClient.Autoload;
using Dto = WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Session;

public partial class SessionList : Control
{
    private Button _mainMenuButton = null!;
    private Button _newSessionButton = null!;
    private ItemList _list = null!;
    private Label _statusLabel = null!;

    private ApiSettings _apiSettings = null!;
    private CatalogState _catalogState = null!;
    private List<Dto.GameSession> _sessions = new();
    private Dictionary<Guid, string> _mapNames = new();

    public override void _Ready()
    {
        _mainMenuButton = GetNode<Button>("VBoxContainer/TopRow/MainMenuButton");
        _newSessionButton = GetNode<Button>("VBoxContainer/TopRow/NewSessionButton");
        _list = GetNode<ItemList>("VBoxContainer/List");
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");
        _apiSettings = GetNode<ApiSettings>("/root/ApiSettings");
        _catalogState = GetNode<CatalogState>("/root/CatalogState");

        _mainMenuButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Main/MainMenu.tscn");
        _newSessionButton.Pressed += () =>
        {
            _catalogState.SelectedSessionId = null;
            GetTree().ChangeSceneToFile("res://Scenes/Session/SessionDetail.tscn");
        };
        _list.ItemActivated += OnSessionActivated;

        _ = Load();
    }

    private async Task Load()
    {
        _statusLabel.Text = "Loading...";
        try
        {
            var maps = await _apiSettings.Client.GetAsync<List<Dto.Map>>("/api/maps");
            _mapNames = maps.ToDictionary(m => m.Id, m => m.Name);

            _sessions = await _apiSettings.Client.GetAsync<List<Dto.GameSession>>("/api/sessions");

            _list.Clear();
            foreach (var session in _sessions)
            {
                var mapName = _mapNames.TryGetValue(session.MapId, out var name) ? name : "Unknown map";
                _list.AddItem($"{session.Status} - Round {session.CurrentRound} - {mapName}");
            }

            _statusLabel.Text = _sessions.Count == 0 ? "No sessions found." : string.Empty;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private void OnSessionActivated(long index)
    {
        var session = _sessions[(int)index];
        _catalogState.SelectedSessionId = session.Id;
        GetTree().ChangeSceneToFile("res://Scenes/Session/SessionDetail.tscn");
    }
}
