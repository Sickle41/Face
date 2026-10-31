using Godot;

namespace WarmachineClient.Scenes.Main;

public partial class MainMenu : Control
{
    public override void _Ready()
    {
        GetNode<Button>("VBoxContainer/CatalogButton").Pressed += () =>
            GetTree().ChangeSceneToFile("res://Scenes/Catalog/FactionList.tscn");
        GetNode<Button>("VBoxContainer/ArmiesButton").Pressed += () =>
            GetTree().ChangeSceneToFile("res://Scenes/Army/ArmyList.tscn");
        GetNode<Button>("VBoxContainer/SessionsButton").Pressed += () =>
            GetTree().ChangeSceneToFile("res://Scenes/Session/SessionList.tscn");
        GetNode<Button>("VBoxContainer/MapsButton").Pressed += () =>
            GetTree().ChangeSceneToFile("res://Scenes/Main/MapPicker.tscn");
        GetNode<Button>("VBoxContainer/SettingsButton").Pressed += () =>
            GetTree().ChangeSceneToFile("res://Scenes/Main/Settings.tscn");
    }
}
