using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using WarmachineClient.Autoload;
using WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Catalog;

public partial class FactionList : Control
{
    private const int PageSize = 20;

    private LineEdit _searchInput = null!;
    private Button _searchButton = null!;
    private Button _mainMenuButton = null!;
    private ItemList _list = null!;
    private Label _pageLabel = null!;
    private Button _prevButton = null!;
    private Button _nextButton = null!;
    private Label _statusLabel = null!;

    private ApiSettings _apiSettings = null!;
    private List<Faction> _currentPage = new();
    private int _page = 1;

    public override void _Ready()
    {
        _searchInput = GetNode<LineEdit>("VBoxContainer/SearchRow/SearchInput");
        _searchButton = GetNode<Button>("VBoxContainer/SearchRow/SearchButton");
        _mainMenuButton = GetNode<Button>("VBoxContainer/SearchRow/MainMenuButton");
        _list = GetNode<ItemList>("VBoxContainer/List");
        _pageLabel = GetNode<Label>("VBoxContainer/PageRow/PageLabel");
        _prevButton = GetNode<Button>("VBoxContainer/PageRow/PrevButton");
        _nextButton = GetNode<Button>("VBoxContainer/PageRow/NextButton");
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");
        _apiSettings = GetNode<ApiSettings>("/root/ApiSettings");

        _mainMenuButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Main/MainMenu.tscn");
        _searchButton.Pressed += () => _ = LoadPage(1);
        _searchInput.TextSubmitted += text => _ = LoadPage(1);
        _prevButton.Pressed += () => _ = LoadPage(_page - 1);
        _nextButton.Pressed += () => _ = LoadPage(_page + 1);
        _list.ItemActivated += OnFactionActivated;

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
            var query = $"/api/factions?page={page}&pageSize={PageSize}";
            if (!string.IsNullOrWhiteSpace(_searchInput.Text))
            {
                query += $"&search={Uri.EscapeDataString(_searchInput.Text)}";
            }

            var result = await _apiSettings.Client.GetAsync<PagedResult<Faction>>(query);
            _page = result.Page;
            _currentPage = result.Items;

            _list.Clear();
            foreach (var faction in _currentPage)
            {
                _list.AddItem(faction.Name);
            }

            var totalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)PageSize));
            _pageLabel.Text = $"Page {_page} / {totalPages} ({result.TotalCount} total)";
            _prevButton.Disabled = _page <= 1;
            _nextButton.Disabled = _page >= totalPages;
            _statusLabel.Text = _currentPage.Count == 0 ? "No factions found." : string.Empty;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private void OnFactionActivated(long index)
    {
        var faction = _currentPage[(int)index];
        GetNode<CatalogState>("/root/CatalogState").SelectedFactionId = faction.Id;
        GetTree().ChangeSceneToFile("res://Scenes/Catalog/UnitList.tscn");
    }
}
