using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using WarmachineClient.Api;
using WarmachineClient.Autoload;
using Dto = WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Board;

public partial class RoundControlsPanel : CanvasLayer
{
    private Label _statusRoundLabel = null!;
    private Button _advanceRoundButton = null!;
    private OptionButton _activeParticipantOption = null!;
    private Label _statusLabel = null!;

    private Board _board = null!;
    private ApiSettings _apiSettings = null!;
    private readonly List<Dto.GameParticipant> _participantOptionsOrder = new();

    public override void _Ready()
    {
        _statusRoundLabel = GetNode<Label>("Panel/VBoxContainer/StatusRoundLabel");
        _advanceRoundButton = GetNode<Button>("Panel/VBoxContainer/AdvanceRoundButton");
        _activeParticipantOption = GetNode<OptionButton>(
            "Panel/VBoxContainer/ActiveParticipantRow/ActiveParticipantOption");
        _statusLabel = GetNode<Label>("Panel/VBoxContainer/StatusLabel");

        _apiSettings = GetNode<ApiSettings>("/root/ApiSettings");
        _board = GetParent<Board>();

        _advanceRoundButton.Pressed += () => _ = AdvanceRound();
        _activeParticipantOption.ItemSelected += index => _ = SetActiveParticipant((int)index);

        _board.Connect(Board.SignalName.BoardStateChanged, Callable.From(Refresh));
    }

    private void Refresh()
    {
        var session = _board.Session;
        if (session is null)
        {
            return;
        }

        _statusRoundLabel.Text = $"Status: {session.Status}  Round: {session.CurrentRound}";

        _activeParticipantOption.Clear();
        _participantOptionsOrder.Clear();

        foreach (var participant in _board.Participants)
        {
            _activeParticipantOption.AddItem(_board.GetArmyName(participant.ArmyId));
            _participantOptionsOrder.Add(participant);

            if (session.ActiveParticipantId == participant.Id)
            {
                _activeParticipantOption.Selected = _participantOptionsOrder.Count - 1;
            }
        }
    }

    private async Task AdvanceRound()
    {
        var session = _board.Session;
        if (session is null)
        {
            return;
        }

        _statusLabel.Text = "Advancing...";
        _advanceRoundButton.Disabled = true;

        try
        {
            await _apiSettings.Client.PutAsync($"/api/sessions/{session.Id}", BuildSessionBody(
                session, session.CurrentRound + 1, session.ActiveParticipantId));
            await _apiSettings.Client.PostAsync($"/api/sessions/{session.Id}/reset-activations");

            await _board.RefreshSession();
            _statusLabel.Text = "Round advanced.";
        }
        catch (ApiException ex) when (ex.Status == 409)
        {
            _statusLabel.Text = "Session changed elsewhere - reload and retry.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
        finally
        {
            _advanceRoundButton.Disabled = false;
        }
    }

    private async Task SetActiveParticipant(int index)
    {
        var session = _board.Session;
        if (session is null || index < 0 || index >= _participantOptionsOrder.Count)
        {
            return;
        }

        var participant = _participantOptionsOrder[index];

        _statusLabel.Text = "Updating...";
        try
        {
            await _apiSettings.Client.PutAsync($"/api/sessions/{session.Id}", BuildSessionBody(
                session, session.CurrentRound, participant.Id));

            await _board.RefreshSession();
            _statusLabel.Text = "Active participant set.";
        }
        catch (ApiException ex) when (ex.Status == 409)
        {
            _statusLabel.Text = "Session changed elsewhere - reload and retry.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private static object BuildSessionBody(Dto.GameSession session, int currentRound, Guid? activeParticipantId) => new
    {
        id = session.Id,
        mapId = session.MapId,
        scenarioId = session.ScenarioId,
        status = session.Status,
        currentRound,
        activeParticipantId,
        createdAt = session.CreatedAt,
        updatedAt = session.UpdatedAt,
        concurrencyToken = session.ConcurrencyToken
    };
}
