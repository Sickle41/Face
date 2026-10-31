using System;
using Godot;

namespace WarmachineClient.Autoload;

public partial class CatalogState : Node
{
    public Guid? SelectedFactionId { get; set; }
    public Guid? SelectedUnitId { get; set; }
    public Guid? SelectedMapId { get; set; }
    public Guid? SelectedArmyId { get; set; }
    public Guid? SelectedSessionId { get; set; }
}
