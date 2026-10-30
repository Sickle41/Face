using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using WarmachineClient.Autoload;
using WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Catalog;

public partial class UnitDetail : Control
{
    private Label _statBlockLabel = null!;
    private RichTextLabel _abilitiesLabel = null!;
    private RichTextLabel _spellsLabel = null!;
    private RichTextLabel _featsLabel = null!;
    private RichTextLabel _attachmentsLabel = null!;
    private Button _backButton = null!;
    private Label _statusLabel = null!;

    private ApiSettings _apiSettings = null!;
    private CatalogState _catalogState = null!;

    public override void _Ready()
    {
        _statBlockLabel = GetNode<Label>("VBoxContainer/StatBlockLabel");
        _abilitiesLabel = GetNode<RichTextLabel>("VBoxContainer/AbilitiesLabel");
        _spellsLabel = GetNode<RichTextLabel>("VBoxContainer/SpellsLabel");
        _featsLabel = GetNode<RichTextLabel>("VBoxContainer/FeatsLabel");
        _attachmentsLabel = GetNode<RichTextLabel>("VBoxContainer/AttachmentsLabel");
        _backButton = GetNode<Button>("VBoxContainer/BackButton");
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");
        _apiSettings = GetNode<ApiSettings>("/root/ApiSettings");
        _catalogState = GetNode<CatalogState>("/root/CatalogState");

        _backButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Catalog/UnitList.tscn");

        _ = LoadUnit();
    }

    private async Task LoadUnit()
    {
        var unitId = _catalogState.SelectedUnitId;
        if (!unitId.HasValue)
        {
            _statusLabel.Text = "No unit selected.";
            return;
        }

        _statusLabel.Text = "Loading...";
        try
        {
            var unit = await _apiSettings.Client.GetAsync<UnitDefinition>($"/api/units/{unitId}");
            _statBlockLabel.Text =
                $"{unit.Name}\n" +
                $"{unit.Category} - {unit.BaseSize} base - {unit.PointCost} pts\n" +
                $"SPD {unit.Speed}  MAT {unit.MeleeAttack}  RAT {unit.RangedAttack}  MAT(Arc) {unit.ArcaneAttack}\n" +
                $"DEF {unit.Defense}  ARM {unit.Armor}  DMG {unit.DamageCapacity}";

            var abilities = await _apiSettings.Client.GetAsync<List<Ability>>($"/api/units/{unitId}/abilities");
            _abilitiesLabel.Text = "Abilities:\n" + FormatNamed(abilities.Select(a => (a.Name, a.Description)));

            var spells = await _apiSettings.Client.GetAsync<List<Spell>>($"/api/units/{unitId}/spells");
            _spellsLabel.Text = "Spells:\n" + FormatNamed(spells.Select(s =>
                (s.Name, $"Cost {s.Cost}, Range {s.Range}, POW {s.Pow} - {s.Description}")));

            var feats = await _apiSettings.Client.GetAsync<List<Feat>>($"/api/units/{unitId}/feats");
            _featsLabel.Text = "Feats:\n" + FormatNamed(feats.Select(f => (f.Name, f.Description)));

            var attachments = await _apiSettings.Client.GetAsync<List<UnitAttachment>>($"/api/units/{unitId}/attachments");
            _attachmentsLabel.Text = "Attachments:\n" + FormatNamed(attachments.Select(a =>
                (a.Name, $"{a.PointCost} pts - {a.Description}")));

            _statusLabel.Text = string.Empty;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private static string FormatNamed(IEnumerable<(string Name, string Detail)> items)
    {
        var list = items.ToList();
        return list.Count == 0 ? "  (none)" : string.Join("\n", list.Select(i => $"  {i.Name} - {i.Detail}"));
    }
}
