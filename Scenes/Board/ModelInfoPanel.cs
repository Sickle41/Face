using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using WarmachineClient.Autoload;
using Dto = WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Board;

public partial class ModelInfoPanel : CanvasLayer
{
    private Control _panelRoot = null!;
    private Label _headerLabel = null!;

    private Control _flatDamageSection = null!;
    private SpinBox _damageSpinBox = null!;

    private Control _gridDamageSection = null!;
    private VBoxContainer _gridDamageList = null!;

    private VBoxContainer _statusEffectsList = null!;
    private CheckBox _activatedCheckBox = null!;
    private CheckBox _featCheckBox = null!;
    private CheckBox _destroyedCheckBox = null!;
    private Button _removeButton = null!;
    private Label _statusLabel = null!;

    private Board _board = null!;
    private ApiSettings _apiSettings = null!;

    private Dto.ModelInstance? _model;
    private Dto.UnitDefinition? _unit;
    private readonly Dictionary<Dto.StatusEffect, CheckBox> _statusCheckBoxes = new();

    public override void _Ready()
    {
        _panelRoot = GetNode<Control>("Panel");
        _headerLabel = GetNode<Label>("Panel/VBoxContainer/HeaderLabel");

        _flatDamageSection = GetNode<Control>("Panel/VBoxContainer/FlatDamageSection");
        _damageSpinBox = GetNode<SpinBox>("Panel/VBoxContainer/FlatDamageSection/DamageRow/DamageSpinBox");

        _gridDamageSection = GetNode<Control>("Panel/VBoxContainer/GridDamageSection");
        _gridDamageList = GetNode<VBoxContainer>("Panel/VBoxContainer/GridDamageSection/GridDamageList");

        _statusEffectsList = GetNode<VBoxContainer>("Panel/VBoxContainer/StatusEffectsList");
        _activatedCheckBox = GetNode<CheckBox>("Panel/VBoxContainer/FlagsRow/ActivatedCheckBox");
        _featCheckBox = GetNode<CheckBox>("Panel/VBoxContainer/FlagsRow/FeatCheckBox");
        _destroyedCheckBox = GetNode<CheckBox>("Panel/VBoxContainer/DestroyedCheckBox");
        _removeButton = GetNode<Button>("Panel/VBoxContainer/RemoveButton");
        _statusLabel = GetNode<Label>("Panel/VBoxContainer/StatusLabel");

        _apiSettings = GetNode<ApiSettings>("/root/ApiSettings");
        _board = GetParent<Board>();

        _panelRoot.Visible = false;

        foreach (Dto.StatusEffect effect in Enum.GetValues<Dto.StatusEffect>())
        {
            var checkBox = new CheckBox { Text = effect.ToString() };
            checkBox.Toggled += pressed => _ = SaveDamage();
            _statusEffectsList.AddChild(checkBox);
            _statusCheckBoxes[effect] = checkBox;
        }

        _damageSpinBox.ValueChanged += newValue => _ = SaveFlatDamage();
        _activatedCheckBox.Toggled += pressed => _ = SaveActivation(pressed);
        _featCheckBox.Toggled += pressed => _ = SaveFeat(pressed);
        _destroyedCheckBox.Toggled += pressed => _ = SaveDamage();
        _removeButton.Pressed += () => _ = RemoveModel();

        _board.Connect(Board.SignalName.SelectionChanged, Callable.From(OnSelectionChanged));
    }

    private void OnSelectionChanged()
    {
        var details = _board.GetSelectedModelDetails();
        if (details is null)
        {
            _panelRoot.Visible = false;
            _model = null;
            _unit = null;
            return;
        }

        _model = details.Value.Model;
        _unit = details.Value.Unit;
        _panelRoot.Visible = true;
        _statusLabel.Text = string.Empty;

        _headerLabel.Text = $"{details.Value.ArmyName} - {_unit.Name}";

        var hasGrid = _unit.DamageColumns.Count > 0;
        _flatDamageSection.Visible = !hasGrid;
        _gridDamageSection.Visible = hasGrid;

        if (hasGrid)
        {
            RenderDamageGrid();
        }
        else
        {
            _damageSpinBox.SetValueNoSignal(_model.DamageTaken);
        }

        foreach (var (effect, checkBox) in _statusCheckBoxes)
        {
            checkBox.SetPressedNoSignal(_model.StatusEffects.Contains(effect));
        }

        _activatedCheckBox.SetPressedNoSignal(_model.HasActivated);
        _featCheckBox.SetPressedNoSignal(_model.HasUsedFeat);
        _destroyedCheckBox.SetPressedNoSignal(_model.IsDestroyed);
    }

    private void RenderDamageGrid()
    {
        foreach (var child in _gridDamageList.GetChildren())
        {
            child.QueueFree();
        }

        if (_model is null || _unit is null)
        {
            return;
        }

        for (var columnIndex = 0; columnIndex < _unit.DamageColumns.Count; columnIndex++)
        {
            var template = _unit.DamageColumns[columnIndex];
            var state = columnIndex < _model.DamageGrid.Count
                ? _model.DamageGrid[columnIndex]
                : new Dto.DamageColumnState { Name = template.Name, BoxesFilled = 0 };

            var row = new HBoxContainer();
            row.AddChild(new Label { Text = template.Name, CustomMinimumSize = new Vector2(80, 0) });

            for (var boxIndex = 0; boxIndex < template.TotalBoxes; boxIndex++)
            {
                var capturedColumnIndex = columnIndex;
                var capturedBoxIndex = boxIndex;
                var box = new Button
                {
                    ToggleMode = true,
                    ButtonPressed = boxIndex < state.BoxesFilled,
                    CustomMinimumSize = new Vector2(18, 18)
                };
                box.Pressed += () => _ = OnDamageBoxPressed(capturedColumnIndex, capturedBoxIndex);
                row.AddChild(box);
            }

            _gridDamageList.AddChild(row);
        }
    }

    private async Task OnDamageBoxPressed(int columnIndex, int boxIndex)
    {
        if (_model is null)
        {
            return;
        }

        while (_model.DamageGrid.Count <= columnIndex)
        {
            _model.DamageGrid.Add(new Dto.DamageColumnState());
        }

        var column = _model.DamageGrid[columnIndex];
        column.BoxesFilled = boxIndex + 1 == column.BoxesFilled ? boxIndex : boxIndex + 1;

        RenderDamageGrid();
        await SaveDamageGrid();
    }

    private async Task SaveFlatDamage()
    {
        if (_model is null)
        {
            return;
        }

        _model.DamageTaken = (int)_damageSpinBox.Value;
        await SaveDamage();
    }

    private async Task SaveDamage()
    {
        if (_model is null)
        {
            return;
        }

        _model.IsDestroyed = _destroyedCheckBox.ButtonPressed;
        _model.StatusEffects = _statusCheckBoxes
            .Where(pair => pair.Value.ButtonPressed)
            .Select(pair => pair.Key)
            .ToList();

        try
        {
            await _apiSettings.Client.PatchAsync($"/api/models/{_model.Id}/damage", new
            {
                damageTaken = _model.DamageTaken,
                isDestroyed = _model.IsDestroyed,
                statusEffects = _model.StatusEffects
            });
            _statusLabel.Text = "Saved.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private async Task SaveDamageGrid()
    {
        if (_model is null)
        {
            return;
        }

        try
        {
            await _apiSettings.Client.PatchAsync($"/api/models/{_model.Id}/damage-grid", _model.DamageGrid);
            _statusLabel.Text = "Saved.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private async Task SaveActivation(bool activated)
    {
        if (_model is null)
        {
            return;
        }

        _model.HasActivated = activated;
        try
        {
            await _apiSettings.Client.PatchAsync(
                $"/api/models/{_model.Id}/activation", new { hasActivated = activated });
            _statusLabel.Text = "Saved.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private async Task SaveFeat(bool usedFeat)
    {
        if (_model is null)
        {
            return;
        }

        _model.HasUsedFeat = usedFeat;
        try
        {
            await _apiSettings.Client.PatchAsync(
                $"/api/models/{_model.Id}/feat", new { hasUsedFeat = usedFeat });
            _statusLabel.Text = "Saved.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private async Task RemoveModel()
    {
        _statusLabel.Text = "Removing...";
        try
        {
            await _board.RemoveSelectedModel();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
    }
}
