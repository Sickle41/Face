using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using WarmachineClient.Autoload;
using WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Board;

public partial class Board : Node2D
{
    public const float PixelsPerUnit = 0.5f;

    private static readonly PackedScene TerrainFeatureScene =
        GD.Load<PackedScene>("res://Scenes/Board/TerrainFeatureView.tscn");
    private static readonly PackedScene DeploymentZoneScene =
        GD.Load<PackedScene>("res://Scenes/Board/DeploymentZoneView.tscn");

    private Polygon2D _background = null!;
    private Camera2D _camera = null!;
    private Node2D _features = null!;
    private ApiSettings _apiSettings = null!;

    public override void _Ready()
    {
        _background = GetNode<Polygon2D>("Background");
        _camera = GetNode<Camera2D>("Camera");
        _features = GetNode<Node2D>("Features");
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
