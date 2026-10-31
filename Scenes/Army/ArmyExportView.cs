using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using WarmachineClient.Autoload;
using Dto = WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Army;

public partial class ArmyExportView : Control
{
    private RichTextLabel _contentLabel = null!;
    private Button _backButton = null!;
    private Label _statusLabel = null!;

    private ApiSettings _apiSettings = null!;
    private CatalogState _catalogState = null!;

    public override void _Ready()
    {
        _contentLabel = GetNode<RichTextLabel>("VBoxContainer/ContentLabel");
        _backButton = GetNode<Button>("VBoxContainer/BackButton");
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");
        _apiSettings = GetNode<ApiSettings>("/root/ApiSettings");
        _catalogState = GetNode<CatalogState>("/root/CatalogState");

        _backButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Army/ArmyEditor.tscn");

        _ = Load();
    }

    private async Task Load()
    {
        var armyId = _catalogState.SelectedArmyId;
        if (!armyId.HasValue)
        {
            _statusLabel.Text = "No army selected.";
            return;
        }

        _statusLabel.Text = "Loading...";
        try
        {
            var export = await _apiSettings.Client.GetAsync<Dto.ArmyExport>($"/api/armies/{armyId}/export");

            var lines = new List<string>
            {
                export.ArmyName,
                $"{export.FactionName} - {export.TotalPoints} / {export.PointLimit} pts",
                string.Empty
            };

            foreach (var entry in export.Entries)
            {
                var line = $"{entry.Quantity}x {entry.UnitName} ({entry.Category}, {entry.PointCost}pts)";
                if (!string.IsNullOrEmpty(entry.AttachmentName))
                {
                    line += $" [+{entry.AttachmentName}]";
                }

                if (!string.IsNullOrEmpty(entry.BattlegroupCasterName))
                {
                    line += $" [Battlegroup: {entry.BattlegroupCasterName}]";
                }

                lines.Add(line);
            }

            _contentLabel.Text = string.Join("\n", lines);
            _statusLabel.Text = string.Empty;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
    }
}
