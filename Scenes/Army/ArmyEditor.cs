using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using WarmachineClient.Api;
using WarmachineClient.Autoload;
using Dto = WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Army;

public partial class ArmyEditor : Control
{
    private Label _titleLabel = null!;
    private LineEdit _nameInput = null!;
    private OptionButton _factionOption = null!;
    private SpinBox _pointLimitInput = null!;
    private Button _saveButton = null!;
    private Button _deleteButton = null!;
    private Button _backButton = null!;
    private Label _statusLabel = null!;

    private Control _entriesSection = null!;
    private ItemList _entriesList = null!;
    private Button _addEntryButton = null!;
    private Button _removeEntryButton = null!;
    private Label _entriesStatusLabel = null!;
    private ArmyEntryEditor _entryEditor = null!;
    private Button _exportButton = null!;
    private Label _pointsSummaryLabel = null!;
    private Button _battlegroupsToggleButton = null!;
    private Label _battlegroupsContentLabel = null!;

    private ApiSettings _apiSettings = null!;
    private CatalogState _catalogState = null!;

    private List<Dto.Faction> _factions = new();
    private List<Dto.UnitDefinition> _factionUnits = new();
    private List<Dto.ArmyEntry> _entries = new();
    private readonly Dictionary<Guid, string> _attachmentNames = new();
    private readonly HashSet<Guid> _attachmentsFetchedForUnit = new();
    private Dto.Army? _army;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("VBoxContainer/TitleLabel");
        _nameInput = GetNode<LineEdit>("VBoxContainer/NameRow/NameInput");
        _factionOption = GetNode<OptionButton>("VBoxContainer/FactionRow/FactionOption");
        _pointLimitInput = GetNode<SpinBox>("VBoxContainer/PointLimitRow/PointLimitInput");
        _saveButton = GetNode<Button>("VBoxContainer/ButtonRow/SaveButton");
        _deleteButton = GetNode<Button>("VBoxContainer/ButtonRow/DeleteButton");
        _backButton = GetNode<Button>("VBoxContainer/ButtonRow/BackButton");
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");

        _entriesSection = GetNode<Control>("VBoxContainer/EntriesSection");
        _entriesList = GetNode<ItemList>("VBoxContainer/EntriesSection/EntriesList");
        _addEntryButton = GetNode<Button>("VBoxContainer/EntriesSection/EntryButtonRow/AddEntryButton");
        _removeEntryButton = GetNode<Button>("VBoxContainer/EntriesSection/EntryButtonRow/RemoveEntryButton");
        _entriesStatusLabel = GetNode<Label>("VBoxContainer/EntriesSection/EntriesStatusLabel");
        _entryEditor = GetNode<ArmyEntryEditor>("EntryEditorPopup");
        _exportButton = GetNode<Button>("VBoxContainer/ButtonRow/ExportButton");
        _pointsSummaryLabel = GetNode<Label>("VBoxContainer/EntriesSection/PointsSummaryLabel");
        _battlegroupsToggleButton = GetNode<Button>("VBoxContainer/EntriesSection/BattlegroupsHeader/ToggleBattlegroupsButton");
        _battlegroupsContentLabel = GetNode<Label>("VBoxContainer/EntriesSection/BattlegroupsContent");

        _apiSettings = GetNode<ApiSettings>("/root/ApiSettings");
        _catalogState = GetNode<CatalogState>("/root/CatalogState");

        _saveButton.Pressed += () => _ = Save();
        _deleteButton.Pressed += () => _ = Delete();
        _backButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Army/ArmyList.tscn");
        _addEntryButton.Pressed += OnAddEntryPressed;
        _removeEntryButton.Pressed += () => _ = RemoveSelectedEntry();
        _entriesList.ItemActivated += OnEntryActivated;
        _entryEditor.Connect(ArmyEntryEditor.SignalName.Saved, Callable.From(() => _ = LoadEntries()));
        _exportButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Army/ArmyExportView.tscn");
        _battlegroupsToggleButton.Pressed += () =>
        {
            _battlegroupsContentLabel.Visible = !_battlegroupsContentLabel.Visible;
            _battlegroupsToggleButton.Text = _battlegroupsContentLabel.Visible
                ? "Battlegroups ▾"
                : "Battlegroups ▸";
        };

        _ = Load();
    }

    private async Task Load()
    {
        _statusLabel.Text = "Loading...";
        try
        {
            var factionResult = await _apiSettings.Client.GetAsync<Dto.PagedResult<Dto.Faction>>(
                "/api/factions?pageSize=200");
            _factions = factionResult.Items;

            _factionOption.Clear();
            foreach (var faction in _factions)
            {
                _factionOption.AddItem(faction.Name);
            }

            var armyId = _catalogState.SelectedArmyId;
            if (armyId.HasValue)
            {
                _army = await _apiSettings.Client.GetAsync<Dto.Army>($"/api/armies/{armyId}");
                _titleLabel.Text = $"Editing: {_army.Name}";
                _nameInput.Text = _army.Name;
                _pointLimitInput.Value = _army.PointLimit;

                var factionIndex = _factions.FindIndex(f => f.Id == _army.FactionId);
                if (factionIndex >= 0)
                {
                    _factionOption.Selected = factionIndex;
                }

                _deleteButton.Visible = true;
                _entriesSection.Visible = true;
                await LoadEntries();
            }
            else
            {
                _titleLabel.Text = "New army";
                _deleteButton.Visible = false;
                _entriesSection.Visible = false;
            }

            _statusLabel.Text = string.Empty;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private async Task LoadEntries()
    {
        if (_army is null)
        {
            return;
        }

        _entriesStatusLabel.Text = "Loading entries...";
        try
        {
            var unitsResult = await _apiSettings.Client.GetAsync<Dto.PagedResult<Dto.UnitDefinition>>(
                $"/api/units?factionId={_army.FactionId}&pageSize=200");
            _factionUnits = unitsResult.Items;

            _entries = await _apiSettings.Client.GetAsync<List<Dto.ArmyEntry>>(
                $"/api/armies/{_army.Id}/entries");

            _entriesList.Clear();
            foreach (var entry in _entries)
            {
                var unitName = _factionUnits.FirstOrDefault(u => u.Id == entry.UnitDefinitionId)?.Name ?? "Unknown";
                var label = $"{entry.Quantity}x {unitName}";

                if (entry.UnitAttachmentId.HasValue)
                {
                    var attachmentName = await ResolveAttachmentName(entry.UnitDefinitionId, entry.UnitAttachmentId.Value);
                    label += $" [+{attachmentName}]";
                }

                if (entry.BattlegroupCasterEntryId.HasValue)
                {
                    var casterEntry = _entries.FirstOrDefault(e => e.Id == entry.BattlegroupCasterEntryId.Value);
                    var casterUnitName = casterEntry is not null
                        ? _factionUnits.FirstOrDefault(u => u.Id == casterEntry.UnitDefinitionId)?.Name ?? "Unknown"
                        : "Unknown";
                    label += $" [battlegroup: {casterUnitName}]";
                }

                _entriesList.AddItem(label);
            }

            _entriesStatusLabel.Text = _entries.Count == 0 ? "No entries yet." : string.Empty;

            await RefreshPointsSummary();
            await RefreshBattlegroups();
        }
        catch (Exception ex)
        {
            _entriesStatusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private async Task RefreshPointsSummary()
    {
        if (_army is null)
        {
            return;
        }

        try
        {
            var summary = await _apiSettings.Client.GetAsync<Dto.ArmyPointsSummary>(
                $"/api/armies/{_army.Id}/points-summary");
            _pointsSummaryLabel.Text = $"{summary.TotalPoints} / {summary.PointLimit} pts";

            if (summary.IsOverLimit)
            {
                _pointsSummaryLabel.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f));
            }
            else
            {
                _pointsSummaryLabel.RemoveThemeColorOverride("font_color");
            }
        }
        catch (Exception ex)
        {
            _pointsSummaryLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private async Task RefreshBattlegroups()
    {
        if (_army is null)
        {
            return;
        }

        try
        {
            var battlegroups = await _apiSettings.Client.GetAsync<List<Dto.Battlegroup>>(
                $"/api/armies/{_army.Id}/battlegroups");

            var lines = new List<string>();
            foreach (var group in battlegroups)
            {
                var casterName = group.Caster is not null
                    ? _factionUnits.FirstOrDefault(u => u.Id == group.Caster.UnitDefinitionId)?.Name ?? "Unknown"
                    : "No caster";
                var memberNames = group.Members.Select(m =>
                    _factionUnits.FirstOrDefault(u => u.Id == m.UnitDefinitionId)?.Name ?? "Unknown");
                lines.Add($"{casterName}: {string.Join(", ", memberNames)}");
            }

            _battlegroupsContentLabel.Text = lines.Count == 0 ? "(none)" : string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            _battlegroupsContentLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private async Task<string> ResolveAttachmentName(Guid unitId, Guid attachmentId)
    {
        if (_attachmentNames.TryGetValue(attachmentId, out var cached))
        {
            return cached;
        }

        if (!_attachmentsFetchedForUnit.Contains(unitId))
        {
            var attachments = await _apiSettings.Client.GetAsync<List<Dto.UnitAttachment>>(
                $"/api/units/{unitId}/attachments");
            foreach (var attachment in attachments)
            {
                _attachmentNames[attachment.Id] = attachment.Name;
            }

            _attachmentsFetchedForUnit.Add(unitId);
        }

        return _attachmentNames.TryGetValue(attachmentId, out var name) ? name : "Unknown";
    }

    private void OnAddEntryPressed()
    {
        if (_army is null)
        {
            return;
        }

        _entryEditor.Configure(_army.Id, _factionUnits, _entries, existingEntry: null);
        _entryEditor.PopupCentered(new Vector2I(420, 320));
    }

    private void OnEntryActivated(long index)
    {
        if (_army is null)
        {
            return;
        }

        var entry = _entries[(int)index];
        _entryEditor.Configure(_army.Id, _factionUnits, _entries, entry);
        _entryEditor.PopupCentered(new Vector2I(420, 320));
    }

    private async Task RemoveSelectedEntry()
    {
        var selected = _entriesList.GetSelectedItems();
        if (selected.Length == 0 || _army is null)
        {
            return;
        }

        var entry = _entries[selected[0]];
        try
        {
            await _apiSettings.Client.DeleteAsync($"/api/army-entries/{entry.Id}");
            await LoadEntries();
        }
        catch (Exception ex)
        {
            _entriesStatusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private async Task Save()
    {
        if (_factionOption.Selected < 0 || _factionOption.Selected >= _factions.Count)
        {
            _statusLabel.Text = "Select a faction.";
            return;
        }

        var factionId = _factions[_factionOption.Selected].Id;
        var name = _nameInput.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            _statusLabel.Text = "Name is required.";
            return;
        }

        _statusLabel.Text = "Saving...";
        _saveButton.Disabled = true;

        try
        {
            if (_army is null)
            {
                var created = await _apiSettings.Client.PostAsync<Dto.Army>("/api/armies", new
                {
                    name,
                    factionId,
                    pointLimit = (int)_pointLimitInput.Value
                });
                _army = created;
                _catalogState.SelectedArmyId = created.Id;
                _titleLabel.Text = $"Editing: {created.Name}";
                _deleteButton.Visible = true;
                _entriesSection.Visible = true;
                _statusLabel.Text = "Created.";
                await LoadEntries();
            }
            else
            {
                var updated = new Dto.Army
                {
                    Id = _army.Id,
                    Name = name,
                    FactionId = factionId,
                    PointLimit = (int)_pointLimitInput.Value,
                    OwnerAccountId = _army.OwnerAccountId,
                    CreatedAt = _army.CreatedAt,
                    UpdatedAt = _army.UpdatedAt,
                    ConcurrencyToken = _army.ConcurrencyToken
                };

                await _apiSettings.Client.PutAsync($"/api/armies/{_army.Id}", updated);
                _army = await _apiSettings.Client.GetAsync<Dto.Army>($"/api/armies/{_army.Id}");
                _titleLabel.Text = $"Editing: {_army.Name}";
                _statusLabel.Text = "Saved.";
                await RefreshPointsSummary();
            }
        }
        catch (ApiException ex) when (ex.Status == 409)
        {
            _statusLabel.Text = "This army changed elsewhere - reload and retry.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
        finally
        {
            _saveButton.Disabled = false;
        }
    }

    private async Task Delete()
    {
        if (_army is null)
        {
            return;
        }

        _statusLabel.Text = "Deleting...";
        _deleteButton.Disabled = true;

        try
        {
            await _apiSettings.Client.DeleteAsync($"/api/armies/{_army.Id}");
            GetTree().ChangeSceneToFile("res://Scenes/Army/ArmyList.tscn");
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
            _deleteButton.Disabled = false;
        }
    }
}
