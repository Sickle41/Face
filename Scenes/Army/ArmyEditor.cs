using Godot;
using WarmachineClient.Autoload;

namespace WarmachineClient.Scenes.Army;

public partial class ArmyEditor : Control
{
    private Label _titleLabel = null!;
    private Button _backButton = null!;
    private CatalogState _catalogState = null!;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("VBoxContainer/TitleLabel");
        _backButton = GetNode<Button>("VBoxContainer/BackButton");
        _catalogState = GetNode<CatalogState>("/root/CatalogState");

        _titleLabel.Text = _catalogState.SelectedArmyId.HasValue
            ? $"Editing army {_catalogState.SelectedArmyId}"
            : "New army";

        _backButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Army/ArmyList.tscn");
    }
}
