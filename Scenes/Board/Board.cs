using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using WarmachineClient.Autoload;
using WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Board;

public partial class Board : Node2D
{
    [Signal]
    public delegate void SelectionChangedEventHandler();

    public const float PixelsPerUnit = 0.5f;

    private static readonly PackedScene TerrainFeatureScene =
        GD.Load<PackedScene>("res://Scenes/Board/TerrainFeatureView.tscn");
    private static readonly PackedScene DeploymentZoneScene =
        GD.Load<PackedScene>("res://Scenes/Board/DeploymentZoneView.tscn");
    private static readonly PackedScene ModelInstanceScene =
        GD.Load<PackedScene>("res://Scenes/Board/ModelInstanceView.tscn");

    private static readonly Color[] ParticipantColors =
    {
        new(0.85f, 0.2f, 0.2f),
        new(0.2f, 0.4f, 0.85f),
        new(0.2f, 0.75f, 0.3f),
        new(0.85f, 0.75f, 0.2f)
    };

    private Polygon2D _background = null!;
    private Camera2D _camera = null!;
    private Node2D _features = null!;
    private Node2D _models = null!;
    private ApiSettings _apiSettings = null!;

    private GameSession? _session;
    private List<GameParticipant> _participants = new();
    private readonly Dictionary<Guid, ArmyEntry> _entriesById = new();
    private readonly Dictionary<Guid, UnitDefinition> _unitsById = new();
    private readonly Dictionary<Guid, Color> _armyColors = new();
    private readonly Dictionary<Guid, string> _armyNames = new();
    private List<ModelInstance> _modelInstances = new();
    private ModelInstanceView? _selectedModelView;

    public Guid? SelectedModelInstanceId => _selectedModelView?.ModelId;

    public override void _Ready()
    {
        _background = GetNode<Polygon2D>("Background");
        _camera = GetNode<Camera2D>("Camera");
        _features = GetNode<Node2D>("Features");
        _models = GetNode<Node2D>("Models");
        _apiSettings = GetNode<ApiSettings>("/root/ApiSettings");
    }

    public async Task LoadMap(Guid mapId)
    {
        var map = await _apiSettings.Client.GetAsync<Map>($"/api/maps/{mapId}");
        ApplyMap(map);

        var terrain = await _apiSettings.Client.GetAsync<List<TerrainFeature>>($"/api/maps/{mapId}/terrain");
        var zones = await _apiSettings.Client.GetAsync<List<DeploymentZone>>($"/api/maps/{mapId}/deployment-zones");
        ApplyFeatures(terrain, zones);
    }

    public async Task LoadSession(Guid sessionId)
    {
        _session = await _apiSettings.Client.GetAsync<GameSession>($"/api/sessions/{sessionId}");
        await LoadMap(_session.MapId);

        _participants = await _apiSettings.Client.GetAsync<List<GameParticipant>>(
            $"/api/sessions/{sessionId}/participants");

        _entriesById.Clear();
        _unitsById.Clear();
        _armyColors.Clear();
        _armyNames.Clear();

        var colorIndex = 0;
        foreach (var participant in _participants)
        {
            _armyColors[participant.ArmyId] = ParticipantColors[colorIndex % ParticipantColors.Length];
            colorIndex++;

            if (!_armyNames.ContainsKey(participant.ArmyId))
            {
                var army = await _apiSettings.Client.GetAsync<WarmachineClient.Api.Dto.Army>(
                    $"/api/armies/{participant.ArmyId}");
                _armyNames[participant.ArmyId] = army.Name;
            }

            var entries = await _apiSettings.Client.GetAsync<List<ArmyEntry>>(
                $"/api/armies/{participant.ArmyId}/entries");
            foreach (var entry in entries)
            {
                _entriesById[entry.Id] = entry;

                if (!_unitsById.ContainsKey(entry.UnitDefinitionId))
                {
                    var unit = await _apiSettings.Client.GetAsync<UnitDefinition>(
                        $"/api/units/{entry.UnitDefinitionId}");
                    _unitsById[entry.UnitDefinitionId] = unit;
                }
            }
        }

        await RefreshModels();
    }

    public async Task SpawnMissingModels()
    {
        if (_session is null)
        {
            return;
        }

        var spawnIndex = 0;
        foreach (var participant in _participants)
        {
            var entries = _entriesById.Values.Where(e => e.ArmyId == participant.ArmyId);
            foreach (var entry in entries)
            {
                var existingCount = _modelInstances.Count(m => m.ArmyEntryId == entry.Id);
                var missing = entry.Quantity - existingCount;
                if (missing <= 0 || !_unitsById.TryGetValue(entry.UnitDefinitionId, out var unit))
                {
                    continue;
                }

                for (var i = 0; i < missing; i++)
                {
                    var damageGrid = unit.DamageColumns
                        .Select(column => new DamageColumnState { Name = column.Name, BoxesFilled = 0 })
                        .ToList();

                    await _apiSettings.Client.PostAsync<ModelInstance>(
                        $"/api/sessions/{_session.Id}/models", new
                        {
                            armyEntryId = entry.Id,
                            x = 100 + spawnIndex % 6 * 150,
                            y = 100 + spawnIndex / 6 * 150,
                            facing = 0,
                            damageTaken = 0,
                            isDestroyed = false,
                            statusEffects = new List<StatusEffect>(),
                            damageGrid
                        });

                    spawnIndex++;
                }
            }
        }

        await RefreshModels();
    }

    private async Task RefreshModels()
    {
        if (_session is null)
        {
            return;
        }

        _modelInstances = await _apiSettings.Client.GetAsync<List<ModelInstance>>(
            $"/api/sessions/{_session.Id}/models");

        foreach (var child in _models.GetChildren())
        {
            child.QueueFree();
        }

        _selectedModelView = null;
        EmitSignal(SignalName.SelectionChanged);

        foreach (var model in _modelInstances)
        {
            if (!_entriesById.TryGetValue(model.ArmyEntryId, out var entry) ||
                !_unitsById.TryGetValue(entry.UnitDefinitionId, out var unit))
            {
                continue;
            }

            var color = _armyColors.TryGetValue(entry.ArmyId, out var armyColor) ? armyColor : Colors.Gray;
            var view = ModelInstanceScene.Instantiate<ModelInstanceView>();
            _models.AddChild(view);
            view.SetModel(model, unit, color);
            view.Connect(ModelInstanceView.SignalName.Selected, Callable.From<ModelInstanceView>(OnModelViewSelected));
        }
    }

    private void OnModelViewSelected(ModelInstanceView view)
    {
        if (_selectedModelView == view)
        {
            return;
        }

        _selectedModelView?.SetSelected(false);
        _selectedModelView = view;
        _selectedModelView.SetSelected(true);
        EmitSignal(SignalName.SelectionChanged);
    }

    public (ModelInstance Model, UnitDefinition Unit, string ArmyName)? GetSelectedModelDetails()
    {
        if (_selectedModelView is null)
        {
            return null;
        }

        var model = _modelInstances.FirstOrDefault(m => m.Id == _selectedModelView.ModelId);
        if (model is null ||
            !_entriesById.TryGetValue(model.ArmyEntryId, out var entry) ||
            !_unitsById.TryGetValue(entry.UnitDefinitionId, out var unit))
        {
            return null;
        }

        var armyName = _armyNames.TryGetValue(entry.ArmyId, out var name) ? name : "Unknown";
        return (model, unit, armyName);
    }

    public async Task RemoveSelectedModel()
    {
        if (_selectedModelView is null)
        {
            return;
        }

        var modelId = _selectedModelView.ModelId;
        await _apiSettings.Client.DeleteAsync($"/api/models/{modelId}");
        await RefreshModels();
    }

    private void ApplyFeatures(List<TerrainFeature> terrain, List<DeploymentZone> zones)
    {
        foreach (var child in _features.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var feature in terrain)
        {
            var view = TerrainFeatureScene.Instantiate<TerrainFeatureView>();
            _features.AddChild(view);
            view.SetFeature(feature);
        }

        foreach (var zone in zones)
        {
            var view = DeploymentZoneScene.Instantiate<DeploymentZoneView>();
            _features.AddChild(view);
            view.SetZone(zone);
        }
    }

    private void ApplyMap(Map map)
    {
        var width = (float)map.Width * PixelsPerUnit;
        var height = (float)map.Height * PixelsPerUnit;

        _background.Polygon = new[]
        {
            new Vector2(0, 0),
            new Vector2(width, 0),
            new Vector2(width, height),
            new Vector2(0, height),
        };

        _camera.LimitLeft = 0;
        _camera.LimitTop = 0;
        _camera.LimitRight = (int)width;
        _camera.LimitBottom = (int)height;
        _camera.Position = new Vector2(width / 2f, height / 2f);
    }
}
