using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using WarmachineClient.Autoload;
using Dto = WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Army;

public partial class ArmyEntryEditor : PopupPanel
{
    [Signal]
    public delegate void SavedEventHandler();

    private OptionButton _unitOption = null!;
    private SpinBox _quantityInput = null!;
    private OptionButton _attachmentOption = null!;
    private OptionButton _casterOption = null!;
    private Button _saveButton = null!;
    private Button _cancelButton = null!;
    private Label _statusLabel = null!;

    private ApiSettings _apiSettings = null!;

    private Guid _armyId;
    private List<Dto.UnitDefinition> _units = new();
    private List<Dto.ArmyEntry> _otherEntries = new();
    private readonly Dictionary<int, Guid?> _attachmentByIndex = new();
    private readonly Dictionary<int, Guid?> _casterByIndex = new();
    private Dto.ArmyEntry? _existingEntry;

    public override void _Ready()
    {
        _unitOption = GetNode<OptionButton>("VBoxContainer/UnitRow/UnitOption");
        _quantityInput = GetNode<SpinBox>("VBoxContainer/QuantityRow/QuantityInput");
        _attachmentOption = GetNode<OptionButton>("VBoxContainer/AttachmentRow/AttachmentOption");
        _casterOption = GetNode<OptionButton>("VBoxContainer/CasterRow/CasterOption");
        _saveButton = GetNode<Button>("VBoxContainer/ButtonRow/SaveButton");
        _cancelButton = GetNode<Button>("VBoxContainer/ButtonRow/CancelButton");
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");
        _apiSettings = GetNode<ApiSettings>("/root/ApiSettings");

        _unitOption.ItemSelected += selectedIndex => _ = RefreshAttachments();
        _saveButton.Pressed += () => _ = Save();
        _cancelButton.Pressed += Hide;
    }

    public void Configure(
        Guid armyId,
        List<Dto.UnitDefinition> units,
        List<Dto.ArmyEntry> otherEntries,
        Dto.ArmyEntry? existingEntry)
    {
        _armyId = armyId;
        _units = units;
        _otherEntries = otherEntries;
        _existingEntry = existingEntry;
        _statusLabel.Text = string.Empty;

        _unitOption.Clear();
        foreach (var unit in _units)
        {
            _unitOption.AddItem($"{unit.Name} ({unit.PointCost} pts)");
        }

        PopulateCasterOptions();

        if (existingEntry is not null)
        {
            var unitIndex = _units.FindIndex(u => u.Id == existingEntry.UnitDefinitionId);
            if (unitIndex >= 0)
            {
                _unitOption.Selected = unitIndex;
            }

            _quantityInput.Value = existingEntry.Quantity;
        }
        else
        {
            _quantityInput.Value = 1;
        }

        _ = RefreshAttachments();
    }

    private void PopulateCasterOptions()
    {
        _casterOption.Clear();
        _casterByIndex.Clear();

        _casterOption.AddItem("None");
        _casterByIndex[0] = null;

        var index = 1;
        foreach (var entry in _otherEntries)
        {
            if (_existingEntry is not null && entry.Id == _existingEntry.Id)
            {
                continue;
            }

            var unit = _units.FirstOrDefault(u => u.Id == entry.UnitDefinitionId);
            if (unit is null ||
                (unit.Category != Dto.UnitCategory.Warcaster && unit.Category != Dto.UnitCategory.Warlock))
            {
                continue;
            }

            _casterOption.AddItem(unit.Name);
            _casterByIndex[index] = entry.Id;
            index++;
        }

        if (_existingEntry?.BattlegroupCasterEntryId is Guid casterId &&
            TryFindIndex(_casterByIndex, casterId, out var casterIndex))
        {
            _casterOption.Selected = casterIndex;
        }
    }

    private async Task RefreshAttachments()
    {
        _attachmentOption.Clear();
        _attachmentByIndex.Clear();
        _attachmentOption.AddItem("None");
        _attachmentByIndex[0] = null;

        if (_unitOption.Selected < 0 || _unitOption.Selected >= _units.Count)
        {
            return;
        }

        var unitId = _units[_unitOption.Selected].Id;

        try
        {
            var attachments = await _apiSettings.Client.GetAsync<List<Dto.UnitAttachment>>(
                $"/api/units/{unitId}/attachments");

            var index = 1;
            foreach (var attachment in attachments)
            {
                _attachmentOption.AddItem(attachment.Name);
                _attachmentByIndex[index] = attachment.Id;
                index++;
            }

            if (_existingEntry?.UnitAttachmentId is Guid attachmentId &&
                TryFindIndex(_attachmentByIndex, attachmentId, out var attachmentIndex))
            {
                _attachmentOption.Selected = attachmentIndex;
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed to load attachments: {ex.Message}";
        }
    }

    private async Task Save()
    {
        if (_unitOption.Selected < 0 || _unitOption.Selected >= _units.Count)
        {
            _statusLabel.Text = "Select a unit.";
            return;
        }

        var unitId = _units[_unitOption.Selected].Id;
        var attachmentId = _attachmentByIndex.TryGetValue(_attachmentOption.Selected, out var attId) ? attId : null;
        var casterId = _casterByIndex.TryGetValue(_casterOption.Selected, out var capId) ? capId : null;

        _statusLabel.Text = "Saving...";
        _saveButton.Disabled = true;

        try
        {
            if (_existingEntry is null)
            {
                await _apiSettings.Client.PostAsync<Dto.ArmyEntry>($"/api/armies/{_armyId}/entries", new
                {
                    unitDefinitionId = unitId,
                    quantity = (int)_quantityInput.Value,
                    unitAttachmentId = attachmentId,
                    battlegroupCasterEntryId = casterId
                });
            }
            else
            {
                await _apiSettings.Client.PutAsync($"/api/army-entries/{_existingEntry.Id}", new
                {
                    id = _existingEntry.Id,
                    armyId = _armyId,
                    unitDefinitionId = unitId,
                    quantity = (int)_quantityInput.Value,
                    unitAttachmentId = attachmentId,
                    battlegroupCasterEntryId = casterId
                });
            }

            EmitSignal(SignalName.Saved);
            Hide();
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

    private static bool TryFindIndex(Dictionary<int, Guid?> map, Guid value, out int index)
    {
        foreach (var pair in map)
        {
            if (pair.Value == value)
            {
                index = pair.Key;
                return true;
            }
        }

        index = -1;
        return false;
    }
}
