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

    private ApiSettings _apiSettings = null!;
    private CatalogState _catalogState = null!;

    private List<Dto.Faction> _factions = new();
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
        _apiSettings = GetNode<ApiSettings>("/root/ApiSettings");
        _catalogState = GetNode<CatalogState>("/root/CatalogState");

        _saveButton.Pressed += () => _ = Save();
        _deleteButton.Pressed += () => _ = Delete();
        _backButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Army/ArmyList.tscn");

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
            }
            else
            {
                _titleLabel.Text = "New army";
                _deleteButton.Visible = false;
            }

            _statusLabel.Text = string.Empty;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
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
                _statusLabel.Text = "Created.";
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
