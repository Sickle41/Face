using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using WarmachineClient.Autoload;
using Dto = WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Session;

public partial class SessionSummaryView : Control
{
    private Label _headerLabel = null!;
    private VBoxContainer _participantsList = null!;
    private VBoxContainer _logCountsList = null!;
    private Button _backButton = null!;
    private Label _statusLabel = null!;

    private ApiSettings _apiSettings = null!;
    private CatalogState _catalogState = null!;

    public override void _Ready()
    {
        _headerLabel = GetNode<Label>("VBoxContainer/HeaderLabel");
        _participantsList = GetNode<VBoxContainer>("VBoxContainer/ParticipantsList");
        _logCountsList = GetNode<VBoxContainer>("VBoxContainer/LogCountsList");
        _backButton = GetNode<Button>("VBoxContainer/BackButton");
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");

        _apiSettings = GetNode<ApiSettings>("/root/ApiSettings");
        _catalogState = GetNode<CatalogState>("/root/CatalogState");

        _backButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Session/SessionDetail.tscn");

        _ = Load();
    }

    private async Task Load()
    {
        var sessionId = _catalogState.SelectedSessionId;
        if (!sessionId.HasValue)
        {
            _statusLabel.Text = "No session selected.";
            return;
        }

        _statusLabel.Text = "Loading...";
        try
        {
            var summary = await _apiSettings.Client.GetAsync<Dto.GameSessionSummary>(
                $"/api/sessions/{sessionId}/summary");

            _headerLabel.Text =
                $"Status: {summary.Status}  Round: {summary.CurrentRound}  " +
                $"Scenario: {summary.ScenarioName ?? "None"}";

            foreach (var child in _participantsList.GetChildren())
            {
                child.QueueFree();
            }

            foreach (var participant in summary.Participants)
            {
                _participantsList.AddChild(new Label
                {
                    Text = $"{participant.ArmyName}: {participant.VictoryPoints} VP, " +
                           $"{participant.ModelsDestroyed}/{participant.ModelCount} destroyed, " +
                           $"{participant.TotalDamageTaken} dmg"
                });
            }

            foreach (var child in _logCountsList.GetChildren())
            {
                child.QueueFree();
            }

            foreach (var pair in summary.LogEntryCountsByType.OrderBy(entry => entry.Key))
            {
                _logCountsList.AddChild(new Label { Text = $"{pair.Key}: {pair.Value}" });
            }

            _statusLabel.Text = string.Empty;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
    }
}
