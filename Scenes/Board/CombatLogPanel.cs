using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using WarmachineClient.Autoload;
using Dto = WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Board;

public partial class CombatLogPanel : CanvasLayer
{
    private Button _toggleButton = null!;
    private Control _content = null!;
    private VBoxContainer _entriesList = null!;
    private OptionButton _eventTypeOption = null!;
    private OptionButton _actorOption = null!;
    private OptionButton _targetOption = null!;
    private LineEdit _descriptionInput = null!;
    private Button _addButton = null!;
    private Label _statusLabel = null!;

    private Board _board = null!;
    private ApiSettings _apiSettings = null!;

    private readonly List<Guid?> _actorOptionIds = new();
    private readonly List<Guid?> _targetOptionIds = new();

    public override void _Ready()
    {
        _toggleButton = GetNode<Button>("Panel/VBoxContainer/ToggleButton");
        _content = GetNode<Control>("Panel/VBoxContainer/Content");
        _entriesList = GetNode<VBoxContainer>("Panel/VBoxContainer/Content/EntriesScroll/EntriesList");
        _eventTypeOption = GetNode<OptionButton>("Panel/VBoxContainer/Content/EventTypeOption");
        _actorOption = GetNode<OptionButton>("Panel/VBoxContainer/Content/ActorRow/ActorOption");
        _targetOption = GetNode<OptionButton>("Panel/VBoxContainer/Content/TargetRow/TargetOption");
        _descriptionInput = GetNode<LineEdit>("Panel/VBoxContainer/Content/DescriptionInput");
        _addButton = GetNode<Button>("Panel/VBoxContainer/Content/AddButton");
        _statusLabel = GetNode<Label>("Panel/VBoxContainer/Content/StatusLabel");

        _apiSettings = GetNode<ApiSettings>("/root/ApiSettings");
        _board = GetParent<Board>();

        _content.Visible = false;
        _toggleButton.Pressed += () =>
        {
            _content.Visible = !_content.Visible;
            _toggleButton.Text = _content.Visible ? "Combat Log ▾" : "Combat Log ▸";
        };

        _eventTypeOption.Clear();
        foreach (var eventType in Enum.GetValues<Dto.CombatLogEventType>())
        {
            _eventTypeOption.AddItem(eventType.ToString());
        }

        _addButton.Pressed += () => _ = AddEntry();

        _board.Connect(Board.SignalName.BoardStateChanged, Callable.From(Refresh));
    }

    private void Refresh()
    {
        PopulateModelOptions();
        _ = LoadEntries();
    }

    private void PopulateModelOptions()
    {
        _actorOption.Clear();
        _actorOptionIds.Clear();
        _actorOption.AddItem("None");
        _actorOptionIds.Add(null);

        _targetOption.Clear();
        _targetOptionIds.Clear();
        _targetOption.AddItem("None");
        _targetOptionIds.Add(null);

        foreach (var model in _board.ModelInstances)
        {
            var label = _board.GetModelLabel(model.Id);
            _actorOption.AddItem(label);
            _actorOptionIds.Add(model.Id);
            _targetOption.AddItem(label);
            _targetOptionIds.Add(model.Id);
        }
    }

    private async Task LoadEntries()
    {
        if (_board.Session is null)
        {
            return;
        }

        try
        {
            var entries = await _apiSettings.Client.GetAsync<List<Dto.CombatLogEntry>>(
                $"/api/sessions/{_board.Session.Id}/log");

            foreach (var child in _entriesList.GetChildren())
            {
                child.QueueFree();
            }

            foreach (var entry in entries)
            {
                _entriesList.AddChild(new Label
                {
                    Text = $"R{entry.Round} {entry.EventType}: {entry.Description}",
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                });
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private async Task AddEntry()
    {
        if (_board.Session is null)
        {
            return;
        }

        if (_eventTypeOption.Selected < 0)
        {
            _statusLabel.Text = "Select an event type.";
            return;
        }

        var eventType = (Dto.CombatLogEventType)_eventTypeOption.Selected;
        var actorId = _actorOptionIds[_actorOption.Selected];
        var targetId = _targetOptionIds[_targetOption.Selected];
        var description = _descriptionInput.Text.Trim();

        _statusLabel.Text = "Adding...";
        _addButton.Disabled = true;

        try
        {
            await _apiSettings.Client.PostAsync<Dto.CombatLogEntry>(
                $"/api/sessions/{_board.Session.Id}/log", new
                {
                    round = _board.Session.CurrentRound,
                    eventType,
                    actorModelInstanceId = actorId,
                    targetModelInstanceId = targetId,
                    description
                });

            _descriptionInput.Text = string.Empty;
            await LoadEntries();
            _statusLabel.Text = string.Empty;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
        finally
        {
            _addButton.Disabled = false;
        }
    }
}
