using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using WarmachineClient.Autoload;
using Dto = WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Session;

public partial class SessionDetail : Control
{
    private Label _titleLabel = null!;

    private Control _createSection = null!;
    private OptionButton _mapOption = null!;
    private OptionButton _scenarioOption = null!;
    private Button _createButton = null!;

    private Control _detailSection = null!;
    private Label _infoLabel = null!;
    private Button _resetActivationsButton = null!;
    private ItemList _participantsList = null!;
    private OptionButton _armyOption = null!;
    private SpinBox _turnOrderInput = null!;
    private Button _addParticipantButton = null!;
    private Button _removeParticipantButton = null!;
    private Label _participantsStatusLabel = null!;

    private Button _backButton = null!;
    private Label _statusLabel = null!;

    private ApiSettings _apiSettings = null!;
    private CatalogState _catalogState = null!;

    private List<Dto.Map> _maps = new();
    private List<Dto.Scenario> _scenarios = new();
    private List<Dto.Army> _armies = new();
    private List<Dto.GameParticipant> _participants = new();
    private Dictionary<Guid, string> _armyNames = new();
    private Dto.GameSession? _session;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("VBoxContainer/TitleLabel");

        _createSection = GetNode<Control>("VBoxContainer/CreateSection");
        _mapOption = GetNode<OptionButton>("VBoxContainer/CreateSection/MapRow/MapOption");
        _scenarioOption = GetNode<OptionButton>("VBoxContainer/CreateSection/ScenarioRow/ScenarioOption");
        _createButton = GetNode<Button>("VBoxContainer/CreateSection/CreateButton");

        _detailSection = GetNode<Control>("VBoxContainer/DetailSection");
        _infoLabel = GetNode<Label>("VBoxContainer/DetailSection/InfoLabel");
        _resetActivationsButton = GetNode<Button>("VBoxContainer/DetailSection/ResetActivationsButton");
        _participantsList = GetNode<ItemList>("VBoxContainer/DetailSection/ParticipantsList");
        _armyOption = GetNode<OptionButton>("VBoxContainer/DetailSection/AddParticipantRow/ArmyOption");
        _turnOrderInput = GetNode<SpinBox>("VBoxContainer/DetailSection/AddParticipantRow/TurnOrderInput");
        _addParticipantButton = GetNode<Button>("VBoxContainer/DetailSection/AddParticipantRow/AddParticipantButton");
        _removeParticipantButton = GetNode<Button>("VBoxContainer/DetailSection/RemoveParticipantButton");
        _participantsStatusLabel = GetNode<Label>("VBoxContainer/DetailSection/ParticipantsStatusLabel");

        _backButton = GetNode<Button>("VBoxContainer/BackButton");
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");

        _apiSettings = GetNode<ApiSettings>("/root/ApiSettings");
        _catalogState = GetNode<CatalogState>("/root/CatalogState");

        _createButton.Pressed += () => _ = CreateSession();
        _resetActivationsButton.Pressed += () => _ = ResetActivations();
        _addParticipantButton.Pressed += () => _ = AddParticipant();
        _removeParticipantButton.Pressed += () => _ = RemoveSelectedParticipant();
        _backButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Session/SessionList.tscn");

        _ = Load();
    }

    private async Task Load()
    {
        _statusLabel.Text = "Loading...";
        try
        {
            _maps = await _apiSettings.Client.GetAsync<List<Dto.Map>>("/api/maps");
            _scenarios = await _apiSettings.Client.GetAsync<List<Dto.Scenario>>("/api/scenarios");

            var armyResult = await _apiSettings.Client.GetAsync<Dto.PagedResult<Dto.Army>>(
                "/api/armies?pageSize=200");
            _armies = armyResult.Items;
            _armyNames = _armies.ToDictionary(a => a.Id, a => a.Name);

            var sessionId = _catalogState.SelectedSessionId;
            if (sessionId.HasValue)
            {
                _session = await _apiSettings.Client.GetAsync<Dto.GameSession>($"/api/sessions/{sessionId}");
                _titleLabel.Text = "Game Session";
                _createSection.Visible = false;
                _detailSection.Visible = true;

                RenderInfo();
                PopulateArmyOptions();
                await LoadParticipants();
            }
            else
            {
                _titleLabel.Text = "New Game Session";
                _createSection.Visible = true;
                _detailSection.Visible = false;

                _mapOption.Clear();
                foreach (var map in _maps)
                {
                    _mapOption.AddItem(map.Name);
                }

                _scenarioOption.Clear();
                _scenarioOption.AddItem("None");
                foreach (var scenario in _scenarios)
                {
                    _scenarioOption.AddItem(scenario.Name);
                }
            }

            _statusLabel.Text = string.Empty;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private void RenderInfo()
    {
        if (_session is null)
        {
            return;
        }

        var mapName = _maps.FirstOrDefault(m => m.Id == _session.MapId)?.Name ?? "Unknown";
        var scenarioName = _session.ScenarioId.HasValue
            ? _scenarios.FirstOrDefault(s => s.Id == _session.ScenarioId.Value)?.Name ?? "Unknown"
            : "None";

        _infoLabel.Text =
            $"Status: {_session.Status}\n" +
            $"Round: {_session.CurrentRound}\n" +
            $"Map: {mapName}\n" +
            $"Scenario: {scenarioName}";
    }

    private void PopulateArmyOptions()
    {
        _armyOption.Clear();
        foreach (var army in _armies)
        {
            _armyOption.AddItem(army.Name);
        }
    }

    private async Task LoadParticipants()
    {
        if (_session is null)
        {
            return;
        }

        _participantsStatusLabel.Text = "Loading participants...";
        try
        {
            _participants = await _apiSettings.Client.GetAsync<List<Dto.GameParticipant>>(
                $"/api/sessions/{_session.Id}/participants");

            _participantsList.Clear();
            foreach (var participant in _participants)
            {
                var armyName = _armyNames.TryGetValue(participant.ArmyId, out var name) ? name : "Unknown";
                _participantsList.AddItem(
                    $"{armyName} - turn {participant.TurnOrder} - {participant.VictoryPoints} VP");
            }

            _participantsStatusLabel.Text = _participants.Count == 0 ? "No participants yet." : string.Empty;
        }
        catch (Exception ex)
        {
            _participantsStatusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private async Task CreateSession()
    {
        if (_mapOption.Selected < 0 || _mapOption.Selected >= _maps.Count)
        {
            _statusLabel.Text = "Select a map.";
            return;
        }

        var mapId = _maps[_mapOption.Selected].Id;
        Guid? scenarioId = _scenarioOption.Selected > 0 && _scenarioOption.Selected - 1 < _scenarios.Count
            ? _scenarios[_scenarioOption.Selected - 1].Id
            : null;

        _statusLabel.Text = "Creating...";
        _createButton.Disabled = true;

        try
        {
            var created = await _apiSettings.Client.PostAsync<Dto.GameSession>("/api/sessions", new
            {
                mapId,
                scenarioId
            });

            _catalogState.SelectedSessionId = created.Id;
            await Load();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
        finally
        {
            _createButton.Disabled = false;
        }
    }

    private async Task ResetActivations()
    {
        if (_session is null)
        {
            return;
        }

        _statusLabel.Text = "Resetting...";
        try
        {
            await _apiSettings.Client.PostAsync($"/api/sessions/{_session.Id}/reset-activations");
            _statusLabel.Text = "Activations reset.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private async Task AddParticipant()
    {
        if (_session is null || _armyOption.Selected < 0 || _armyOption.Selected >= _armies.Count)
        {
            _participantsStatusLabel.Text = "Select an army.";
            return;
        }

        var armyId = _armies[_armyOption.Selected].Id;
        var turnOrder = (int)_turnOrderInput.Value;

        _participantsStatusLabel.Text = "Adding...";
        try
        {
            await _apiSettings.Client.PostAsync<Dto.GameParticipant>(
                $"/api/sessions/{_session.Id}/participants", new
                {
                    armyId,
                    turnOrder
                });
            await LoadParticipants();
        }
        catch (Exception ex)
        {
            _participantsStatusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private async Task RemoveSelectedParticipant()
    {
        var selected = _participantsList.GetSelectedItems();
        if (selected.Length == 0)
        {
            return;
        }

        var participant = _participants[selected[0]];
        try
        {
            await _apiSettings.Client.DeleteAsync($"/api/participants/{participant.Id}");
            await LoadParticipants();
        }
        catch (Exception ex)
        {
            _participantsStatusLabel.Text = $"Failed: {ex.Message}";
        }
    }
}
