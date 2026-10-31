using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using WarmachineClient.Autoload;
using Dto = WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Army;

public partial class ArmyList : Control
{
    private const int PageSize = 20;

    private Button _mainMenuButton = null!;
    private Button _newArmyButton = null!;
    private ItemList _list = null!;
    private Label _pageLabel = null!;
    private Button _prevButton = null!;
    private Button _nextButton = null!;
    private Label _statusLabel = null!;

    private ApiSettings _apiSettings = null!;
    private CatalogState _catalogState = null!;
    private List<Dto.Army> _currentPage = new();
    private Dictionary<Guid, string> _factionNames = new();
    private int _page = 1;

    public override void _Ready()
    {
        _mainMenuButton = GetNode<Button>("VBoxContainer/TopRow/MainMenuButton");
        _newArmyButton = GetNode<Button>("VBoxContainer/TopRow/NewArmyButton");
        _list = GetNode<ItemList>("VBoxContainer/List");
        _pageLabel = GetNode<Label>("VBoxContainer/PageRow/PageLabel");
        _prevButton = GetNode<Button>("VBoxContainer/PageRow/PrevButton");
        _nextButton = GetNode<Button>("VBoxContainer/PageRow/NextButton");
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");
        _apiSettings = GetNode<ApiSettings>("/root/ApiSettings");
        _catalogState = GetNode<CatalogState>("/root/CatalogState");

        _mainMenuButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Main/MainMenu.tscn");
        _newArmyButton.Pressed += () =>
        {
            _catalogState.SelectedArmyId = null;
            GetTree().ChangeSceneToFile("res://Scenes/Army/ArmyEditor.tscn");
        };
        _prevButton.Pressed += () => _ = LoadPage(_page - 1);
        _nextButton.Pressed += () => _ = LoadPage(_page + 1);
        _list.ItemActivated += OnArmyActivated;

        _ = LoadPage(1);
    }

    private async Task LoadPage(int page)
    {
        if (page < 1)
        {
            return;
        }

        _statusLabel.Text = "Loading...";
        try
        {
            if (_factionNames.Count == 0)
            {
                var factions = await _apiSettings.Client.GetAsync<Dto.PagedResult<Dto.Faction>>(
                    "/api/factions?pageSize=200");
                _factionNames = factions.Items.ToDictionary(f => f.Id, f => f.Name);
            }

            var result = await _apiSettings.Client.GetAsync<Dto.PagedResult<Dto.Army>>(
                $"/api/armies?page={page}&pageSize={PageSize}");
            _page = result.Page;
            _currentPage = result.Items;

            _list.Clear();
            foreach (var army in _currentPage)
            {
                var factionName = _factionNames.TryGetValue(army.FactionId, out var name) ? name : "Unknown";
                _list.AddItem($"{army.Name} ({factionName}, {army.PointLimit} pts)");
            }

            var totalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)PageSize));
            _pageLabel.Text = $"Page {_page} / {totalPages} ({result.TotalCount} total)";
            _prevButton.Disabled = _page <= 1;
            _nextButton.Disabled = _page >= totalPages;
            _statusLabel.Text = _currentPage.Count == 0 ? "No armies found." : string.Empty;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private void OnArmyActivated(long index)
    {
        var army = _currentPage[(int)index];
        _catalogState.SelectedArmyId = army.Id;
        GetTree().ChangeSceneToFile("res://Scenes/Army/ArmyEditor.tscn");
    }
}
