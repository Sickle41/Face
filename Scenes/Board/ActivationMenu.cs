using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using WarmachineClient.Autoload;
using WarmachineClient.Rules;
using Dto = WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Board;

public partial class ActivationMenu : CanvasLayer
{
    private enum PendingActionType
    {
        Melee,
        Ranged,
        Spell
    }

    private Control _panelRoot = null!;
    private Label _headerLabel = null!;
    private Label _alreadyActivatedLabel = null!;

    private Control _actionsSection = null!;
    private Button _meleeButton = null!;
    private Button _rangedButton = null!;
    private Control _spellRow = null!;
    private OptionButton _spellOption = null!;
    private Button _castSpellButton = null!;
    private Button _endActivationButton = null!;

    private Control _awaitingTargetSection = null!;
    private Label _awaitingTargetLabel = null!;
    private Button _cancelButton = null!;

    private Label _resultLabel = null!;
    private Button _rollColumnButton = null!;
    private Label _statusLabel = null!;

    private Board _board = null!;
    private ApiSettings _apiSettings = null!;

    private List<Dto.Spell> _attackerSpells = new();

    private bool _awaitingTarget;
    private Guid _actingModelId;
    private PendingActionType _pendingActionType;
    private Dto.Spell? _pendingSpell;

    private Dto.ModelInstance? _pendingTargetModel;
    private Dto.UnitDefinition? _pendingTargetUnit;
    private int _pendingDamageAmount;
    private bool _pendingInflictsKnockdown;

    public override void _Ready()
    {
        _panelRoot = GetNode<Control>("Panel");
        _headerLabel = GetNode<Label>("Panel/VBoxContainer/HeaderLabel");
        _alreadyActivatedLabel = GetNode<Label>("Panel/VBoxContainer/AlreadyActivatedLabel");

        _actionsSection = GetNode<Control>("Panel/VBoxContainer/ActionsSection");
        _meleeButton = GetNode<Button>("Panel/VBoxContainer/ActionsSection/MeleeButton");
        _rangedButton = GetNode<Button>("Panel/VBoxContainer/ActionsSection/RangedButton");
        _spellRow = GetNode<Control>("Panel/VBoxContainer/ActionsSection/SpellRow");
        _spellOption = GetNode<OptionButton>("Panel/VBoxContainer/ActionsSection/SpellRow/SpellOption");
        _castSpellButton = GetNode<Button>("Panel/VBoxContainer/ActionsSection/CastSpellButton");
        _endActivationButton = GetNode<Button>("Panel/VBoxContainer/ActionsSection/EndActivationButton");

        _awaitingTargetSection = GetNode<Control>("Panel/VBoxContainer/AwaitingTargetSection");
        _awaitingTargetLabel = GetNode<Label>("Panel/VBoxContainer/AwaitingTargetSection/AwaitingTargetLabel");
        _cancelButton = GetNode<Button>("Panel/VBoxContainer/AwaitingTargetSection/CancelButton");

        _resultLabel = GetNode<Label>("Panel/VBoxContainer/ResultLabel");
        _rollColumnButton = GetNode<Button>("Panel/VBoxContainer/RollColumnButton");
        _statusLabel = GetNode<Label>("Panel/VBoxContainer/StatusLabel");

        _apiSettings = GetNode<ApiSettings>("/root/ApiSettings");
        _board = GetParent<Board>();

        _meleeButton.Pressed += () => BeginTargeting(PendingActionType.Melee);
        _rangedButton.Pressed += () => BeginTargeting(PendingActionType.Ranged);
        _castSpellButton.Pressed += () => BeginTargeting(PendingActionType.Spell);
        _endActivationButton.Pressed += () => _ = EndActivation();
        _cancelButton.Pressed += CancelTargeting;
        _rollColumnButton.Pressed += () => _ = RollDamageColumn();

        _board.Connect(Board.SignalName.SelectionChanged, Callable.From(OnSelectionChanged));
        _board.Connect(Board.SignalName.BoardStateChanged, Callable.From(OnSelectionChanged));

        RenderForSelection();
    }

    private void OnSelectionChanged()
    {
        if (_awaitingTarget)
        {
            var targetId = _board.SelectedModelInstanceId;
            if (targetId.HasValue)
            {
                _ = ResolveTargetSelection(targetId.Value);
            }

            return;
        }

        RenderForSelection();
    }

    private void RenderForSelection()
    {
        if (_pendingTargetModel is not null)
        {
            _panelRoot.Visible = true;
            _actionsSection.Visible = false;
            _awaitingTargetSection.Visible = false;
            _alreadyActivatedLabel.Visible = false;
            _rollColumnButton.Visible = true;
            return;
        }

        _rollColumnButton.Visible = false;
        _resultLabel.Text = string.Empty;
        _statusLabel.Text = string.Empty;
        _awaitingTargetSection.Visible = false;

        var selectedId = _board.SelectedModelInstanceId;
        if (!selectedId.HasValue)
        {
            _panelRoot.Visible = false;
            return;
        }

        var details = _board.GetModelDetails(selectedId.Value);
        if (details is null)
        {
            _panelRoot.Visible = false;
            return;
        }

        var (model, unit, armyName) = details.Value;
        _panelRoot.Visible = true;
        _headerLabel.Text = $"{armyName}: {unit.Name} {_board.GetModelDesignation(selectedId.Value)}";

        if (model.HasActivated)
        {
            _actionsSection.Visible = false;
            _alreadyActivatedLabel.Visible = true;
            return;
        }

        _alreadyActivatedLabel.Visible = false;
        _actionsSection.Visible = true;
        _rangedButton.Visible = unit.RangedWeaponRof > 0;

        _ = LoadSpellsFor(unit);
    }

    private async Task LoadSpellsFor(Dto.UnitDefinition unit)
    {
        try
        {
            _attackerSpells = await _apiSettings.Client.GetAsync<List<Dto.Spell>>($"/api/units/{unit.Id}/spells");
        }
        catch (Exception)
        {
            _attackerSpells = new List<Dto.Spell>();
        }

        var hasSpells = _attackerSpells.Count > 0;
        _spellRow.Visible = hasSpells;
        _castSpellButton.Visible = hasSpells;

        _spellOption.Clear();
        foreach (var spell in _attackerSpells)
        {
            _spellOption.AddItem($"{spell.Name} (RNG {spell.Range}, POW {spell.Pow})");
        }

        _spellOption.Selected = hasSpells ? 0 : -1;
    }

    private void BeginTargeting(PendingActionType actionType)
    {
        var selectedId = _board.SelectedModelInstanceId;
        if (!selectedId.HasValue)
        {
            return;
        }

        if (actionType == PendingActionType.Spell &&
            (_spellOption.Selected < 0 || _spellOption.Selected >= _attackerSpells.Count))
        {
            _statusLabel.Text = "Select a spell.";
            return;
        }

        _actingModelId = selectedId.Value;
        _pendingActionType = actionType;
        _pendingSpell = actionType == PendingActionType.Spell ? _attackerSpells[_spellOption.Selected] : null;
        _awaitingTarget = true;

        _actionsSection.Visible = false;
        _awaitingTargetSection.Visible = true;
        _awaitingTargetLabel.Text = $"Click a target on the board for this {actionType} attack.";
        _statusLabel.Text = string.Empty;
    }

    private void CancelTargeting()
    {
        _awaitingTarget = false;
        _awaitingTargetSection.Visible = false;
        RenderForSelection();
    }

    private async Task ResolveTargetSelection(Guid targetId)
    {
        if (targetId == _actingModelId)
        {
            _statusLabel.Text = "Pick a different model as the target.";
            return;
        }

        var attackerDetails = _board.GetModelDetails(_actingModelId);
        var targetDetails = _board.GetModelDetails(targetId);
        if (attackerDetails is null || targetDetails is null)
        {
            _statusLabel.Text = "Could not resolve attacker/target.";
            return;
        }

        var (attackerModel, attackerUnit, _) = attackerDetails.Value;
        var (targetModel, targetUnit, _) = targetDetails.Value;

        double rangeInches;
        Dto.CombatLogEventType eventType;
        string verb;

        switch (_pendingActionType)
        {
            case PendingActionType.Melee:
                rangeInches = attackerUnit.MeleeWeaponRange;
                eventType = Dto.CombatLogEventType.MeleeAttack;
                verb = "melee attacks";
                break;
            case PendingActionType.Ranged:
                rangeInches = attackerUnit.RangedWeaponRange;
                eventType = Dto.CombatLogEventType.RangedAttack;
                verb = "shoots";
                break;
            case PendingActionType.Spell:
                rangeInches = _pendingSpell!.Range;
                eventType = Dto.CombatLogEventType.SpellCast;
                verb = $"casts {_pendingSpell.Name} on";
                break;
            default:
                return;
        }

        var distanceInches = RangeCheck.DistanceInInches(attackerModel, targetModel);
        if (distanceInches > rangeInches)
        {
            _statusLabel.Text = $"Out of range ({distanceInches:0.0}\" > {rangeInches}\"). Pick another target or Cancel.";
            return;
        }

        var fromWorld = new Vector2((float)attackerModel.X, (float)attackerModel.Y);
        var toWorld = new Vector2((float)targetModel.X, (float)targetModel.Y);
        if (!LineOfSight.HasClearLineOfSight(fromWorld, toWorld, _board.Terrain))
        {
            _statusLabel.Text = "No line of sight to target. Pick another target or Cancel.";
            return;
        }

        var attackerId = _actingModelId;

        var result = _pendingActionType switch
        {
            PendingActionType.Melee => AttackResolver.ResolveMeleeAttack(attackerUnit, targetUnit),
            PendingActionType.Ranged => AttackResolver.ResolveRangedAttack(attackerUnit, targetUnit),
            PendingActionType.Spell => AttackResolver.ResolveSpellAttack(attackerUnit, _pendingSpell!, targetUnit),
            _ => throw new InvalidOperationException()
        };

        var toHitStat = _pendingActionType switch
        {
            PendingActionType.Melee => attackerUnit.MeleeAttack,
            PendingActionType.Ranged => attackerUnit.RangedAttack,
            PendingActionType.Spell => attackerUnit.ArcaneAttack,
            _ => 0
        };

        var pow = _pendingActionType switch
        {
            PendingActionType.Melee => attackerUnit.MeleeWeaponPow,
            PendingActionType.Ranged => attackerUnit.RangedWeaponPow,
            PendingActionType.Spell => _pendingSpell!.Pow,
            _ => 0
        };

        _awaitingTarget = false;
        _awaitingTargetSection.Visible = false;

        if (!result.Hit)
        {
            _resultLabel.Text =
                $"To-hit: {result.ToHitRoll.Die1}+{result.ToHitRoll.Die2}={result.ToHitRoll.Total} " +
                $"+ {toHitStat} vs DEF {targetUnit.Defense} -> Miss";

            await LogAttack(attackerId, targetId, targetUnit, toHitStat, pow, result, verb, eventType);
            await MarkActivated(attackerId);
            await _board.RefreshModels();
            return;
        }

        _resultLabel.Text =
            $"To-hit: {result.ToHitRoll.Total} vs DEF {targetUnit.Defense} -> Hit. " +
            $"Damage: {result.DamageRoll!.Die1}+{result.DamageRoll.Die2}={result.DamageRoll.Total} " +
            $"- ARM {targetUnit.Armor} = {result.DamageDealt}";

        if (targetUnit.DamageColumns.Count > 0)
        {
            _pendingTargetModel = targetModel;
            _pendingTargetUnit = targetUnit;
            _pendingDamageAmount = result.DamageDealt;
            _pendingInflictsKnockdown = result.InflictsKnockdown;
            _statusLabel.Text = "Hit a warjack/warbeast - roll for damage column.";

            await LogAttack(attackerId, targetId, targetUnit, toHitStat, pow, result, verb, eventType);
            await MarkActivated(attackerId);
            await _board.RefreshModels();
        }
        else
        {
            await LogAttack(attackerId, targetId, targetUnit, toHitStat, pow, result, verb, eventType);
            await MarkActivated(attackerId);
            await ApplyFlatDamage(targetModel, targetUnit, result.DamageDealt, result.InflictsKnockdown);
        }
    }

    private async Task EndActivation()
    {
        var selectedId = _board.SelectedModelInstanceId;
        if (!selectedId.HasValue)
        {
            return;
        }

        try
        {
            await MarkActivated(selectedId.Value);
            await _board.RefreshModels();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private async Task MarkActivated(Guid modelId)
    {
        try
        {
            await _apiSettings.Client.PatchAsync($"/api/models/{modelId}/activation", new { hasActivated = true });
        }
        catch (Exception)
        {
            // The attack result already applied; activation can still be marked via End Activation.
        }
    }

    private async Task RollDamageColumn()
    {
        if (_pendingTargetModel is null || _pendingTargetUnit is null)
        {
            return;
        }

        _rollColumnButton.Disabled = true;
        try
        {
            var grid = _pendingTargetModel.DamageGrid;
            var template = _pendingTargetUnit.DamageColumns;

            var openIndexes = new List<int>();
            for (var i = 0; i < template.Count; i++)
            {
                var filled = i < grid.Count ? grid[i].BoxesFilled : 0;
                if (filled < template[i].TotalBoxes)
                {
                    openIndexes.Add(i);
                }
            }

            if (openIndexes.Count == 0)
            {
                _statusLabel.Text = "All systems already destroyed.";
                return;
            }

            var chosenIndex = openIndexes[Random.Shared.Next(openIndexes.Count)];

            while (grid.Count < template.Count)
            {
                grid.Add(new Dto.DamageColumnState { Name = template[grid.Count].Name, BoxesFilled = 0 });
            }

            ApplyDamageWithRollover(grid, template, chosenIndex, _pendingDamageAmount);
            var column = grid[chosenIndex];

            var allDestroyed = grid.Zip(template, (g, t) => g.BoxesFilled >= t.TotalBoxes).All(x => x);

            await _apiSettings.Client.PatchAsync($"/api/models/{_pendingTargetModel.Id}/damage-grid", grid);

            if (allDestroyed || _pendingInflictsKnockdown)
            {
                var statusEffects = _pendingTargetModel.StatusEffects.ToList();
                if (_pendingInflictsKnockdown && !statusEffects.Contains(Dto.StatusEffect.KnockedDown))
                {
                    statusEffects.Add(Dto.StatusEffect.KnockedDown);
                }

                await _apiSettings.Client.PatchAsync($"/api/models/{_pendingTargetModel.Id}/damage", new
                {
                    damageTaken = _pendingTargetModel.DamageTaken,
                    isDestroyed = allDestroyed,
                    statusEffects
                });
            }

            _statusLabel.Text = $"Rolled column: {template[chosenIndex].Name} " +
                                 $"({column.BoxesFilled}/{template[chosenIndex].TotalBoxes})" +
                                 (allDestroyed ? " - DESTROYED" : string.Empty);

            await LogColumnHit(_pendingTargetModel.Id, template[chosenIndex].Name, column, template[chosenIndex], allDestroyed);

            _pendingTargetModel = null;
            _pendingTargetUnit = null;
            _pendingInflictsKnockdown = false;

            await _board.RefreshModels();
        }
        finally
        {
            _rollColumnButton.Disabled = false;
        }
    }

    private static void ApplyDamageWithRollover(
        List<Dto.DamageColumnState> grid, List<Dto.DamageColumnTemplate> template, int startIndex, int damage)
    {
        var remaining = damage;
        var index = startIndex;
        var columnsVisited = 0;

        while (remaining > 0 && columnsVisited < template.Count)
        {
            var column = grid[index];
            var capacity = template[index].TotalBoxes - column.BoxesFilled;

            if (capacity > 0)
            {
                var applied = Math.Min(capacity, remaining);
                column.BoxesFilled += applied;
                remaining -= applied;
            }

            index = (index + 1) % template.Count;
            columnsVisited++;
        }
    }

    private async Task ApplyFlatDamage(
        Dto.ModelInstance targetModel, Dto.UnitDefinition targetUnit, int damage, bool inflictsKnockdown)
    {
        var newDamage = targetModel.DamageTaken + damage;
        var destroyed = newDamage >= targetUnit.DamageCapacity;

        var statusEffects = targetModel.StatusEffects.ToList();
        if (inflictsKnockdown && !statusEffects.Contains(Dto.StatusEffect.KnockedDown))
        {
            statusEffects.Add(Dto.StatusEffect.KnockedDown);
        }

        await _apiSettings.Client.PatchAsync($"/api/models/{targetModel.Id}/damage", new
        {
            damageTaken = newDamage,
            isDestroyed = destroyed,
            statusEffects
        });

        if (destroyed)
        {
            _statusLabel.Text = "Target destroyed.";
            await LogDestruction(targetModel.Id);
        }

        await _board.RefreshModels();
    }

    private async Task LogAttack(
        Guid attackerId,
        Guid targetId,
        Dto.UnitDefinition targetUnit,
        int toHitStat,
        int pow,
        AttackResult result,
        string verb,
        Dto.CombatLogEventType eventType)
    {
        if (_board.Session is null)
        {
            return;
        }

        var toHitTotal = result.ToHitRoll.Total + toHitStat;
        var toHitText = $"rolled {result.ToHitRoll.Die1}+{result.ToHitRoll.Die2}={result.ToHitRoll.Total} " +
                         $"+{toHitStat} = {toHitTotal} vs DEF {targetUnit.Defense}";

        string description;
        if (!result.Hit)
        {
            description = $"{_board.GetModelLabel(attackerId)} {verb} {_board.GetModelLabel(targetId)} " +
                           $"({toHitText}) and misses.";
        }
        else
        {
            var damageText = $"rolled {result.DamageRoll!.Die1}+{result.DamageRoll.Die2}={result.DamageRoll.Total} " +
                              $"+{pow} -ARM {targetUnit.Armor} = {result.DamageDealt}";
            description = $"{_board.GetModelLabel(attackerId)} {verb} {_board.GetModelLabel(targetId)} " +
                           $"({toHitText}; {damageText}) for {result.DamageDealt} damage.";
        }

        try
        {
            await _apiSettings.Client.PostAsync<Dto.CombatLogEntry>($"/api/sessions/{_board.Session.Id}/log", new
            {
                round = _board.Session.CurrentRound,
                eventType,
                actorModelInstanceId = attackerId,
                targetModelInstanceId = targetId,
                description
            });
        }
        catch (Exception)
        {
            // A logging failure shouldn't hide the attack result that already rendered above.
        }
    }

    private async Task LogColumnHit(
        Guid targetId, string columnName, Dto.DamageColumnState column, Dto.DamageColumnTemplate template, bool destroyed)
    {
        if (_board.Session is null)
        {
            return;
        }

        var description = $"{_board.GetModelLabel(targetId)}'s {columnName} takes damage " +
                           $"({column.BoxesFilled}/{template.TotalBoxes})." +
                           (destroyed ? $" {_board.GetModelLabel(targetId)} is destroyed." : string.Empty);

        try
        {
            await _apiSettings.Client.PostAsync<Dto.CombatLogEntry>($"/api/sessions/{_board.Session.Id}/log", new
            {
                round = _board.Session.CurrentRound,
                eventType = destroyed ? Dto.CombatLogEventType.Destroyed : Dto.CombatLogEventType.Damage,
                actorModelInstanceId = (Guid?)null,
                targetModelInstanceId = targetId,
                description
            });
        }
        catch (Exception)
        {
            // A logging failure shouldn't hide the result already shown in the panel.
        }
    }

    private async Task LogDestruction(Guid targetId)
    {
        if (_board.Session is null)
        {
            return;
        }

        try
        {
            await _apiSettings.Client.PostAsync<Dto.CombatLogEntry>($"/api/sessions/{_board.Session.Id}/log", new
            {
                round = _board.Session.CurrentRound,
                eventType = Dto.CombatLogEventType.Destroyed,
                actorModelInstanceId = (Guid?)null,
                targetModelInstanceId = targetId,
                description = $"{_board.GetModelLabel(targetId)} is destroyed."
            });
        }
        catch (Exception)
        {
            // A logging failure shouldn't hide the result already shown in the panel.
        }
    }
}
