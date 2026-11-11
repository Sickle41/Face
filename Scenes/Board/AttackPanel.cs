using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using WarmachineClient.Autoload;
using WarmachineClient.Rules;
using Dto = WarmachineClient.Api.Dto;

namespace WarmachineClient.Scenes.Board;

public partial class AttackPanel : CanvasLayer
{
    private enum AttackType
    {
        Melee,
        Ranged,
        Spell
    }

    private OptionButton _attackerOption = null!;
    private OptionButton _targetOption = null!;
    private OptionButton _attackTypeOption = null!;
    private Control _spellRow = null!;
    private OptionButton _spellOption = null!;
    private Label _weaponInfoLabel = null!;
    private Button _attackButton = null!;
    private Label _resultLabel = null!;
    private Button _rollColumnButton = null!;
    private Label _statusLabel = null!;

    private Board _board = null!;
    private ApiSettings _apiSettings = null!;

    private readonly List<Guid> _attackerIds = new();
    private readonly List<Guid> _targetIds = new();
    private readonly List<AttackType> _attackTypeChoices = new();
    private List<Dto.Spell> _attackerSpells = new();

    private Dto.ModelInstance? _pendingTargetModel;
    private Dto.UnitDefinition? _pendingTargetUnit;
    private int _pendingDamageAmount;
    private bool _pendingInflictsKnockdown;

    public override void _Ready()
    {
        _attackerOption = GetNode<OptionButton>("Panel/VBoxContainer/AttackerRow/AttackerOption");
        _targetOption = GetNode<OptionButton>("Panel/VBoxContainer/TargetRow/TargetOption");
        _attackTypeOption = GetNode<OptionButton>("Panel/VBoxContainer/AttackTypeRow/AttackTypeOption");
        _spellRow = GetNode<Control>("Panel/VBoxContainer/SpellRow");
        _spellOption = GetNode<OptionButton>("Panel/VBoxContainer/SpellRow/SpellOption");
        _weaponInfoLabel = GetNode<Label>("Panel/VBoxContainer/WeaponInfoLabel");
        _attackButton = GetNode<Button>("Panel/VBoxContainer/AttackButton");
        _resultLabel = GetNode<Label>("Panel/VBoxContainer/ResultLabel");
        _rollColumnButton = GetNode<Button>("Panel/VBoxContainer/RollColumnButton");
        _statusLabel = GetNode<Label>("Panel/VBoxContainer/StatusLabel");

        _apiSettings = GetNode<ApiSettings>("/root/ApiSettings");
        _board = GetParent<Board>();

        _rollColumnButton.Visible = false;
        _spellRow.Visible = false;
        _weaponInfoLabel.Visible = false;

        _attackerOption.ItemSelected += selectedIndex => _ = OnAttackerChanged();
        _attackTypeOption.ItemSelected += index => UpdateAttackTypeUi(_attackTypeChoices[(int)index]);
        _attackButton.Pressed += () => _ = DoAttack();
        _rollColumnButton.Pressed += () => _ = RollDamageColumn();

        _board.Connect(Board.SignalName.BoardStateChanged, Callable.From(Refresh));
    }

    private void Refresh()
    {
        _attackerOption.Clear();
        _attackerIds.Clear();
        _targetOption.Clear();
        _targetIds.Clear();

        foreach (var model in _board.ModelInstances)
        {
            if (model.IsDestroyed)
            {
                continue;
            }

            var label = _board.GetModelLabel(model.Id);
            _attackerOption.AddItem(label);
            _attackerIds.Add(model.Id);
            _targetOption.AddItem(label);
            _targetIds.Add(model.Id);
        }

        _ = OnAttackerChanged();
    }

    private async Task OnAttackerChanged()
    {
        _attackTypeOption.Clear();
        _attackTypeChoices.Clear();
        _attackerSpells.Clear();
        _spellRow.Visible = false;
        _weaponInfoLabel.Visible = false;

        if (_attackerOption.Selected < 0 || _attackerOption.Selected >= _attackerIds.Count)
        {
            return;
        }

        var attackerId = _attackerIds[_attackerOption.Selected];
        var attackerDetails = _board.GetModelDetails(attackerId);
        if (attackerDetails is null)
        {
            return;
        }

        var attackerUnit = attackerDetails.Value.Unit;

        _attackTypeOption.AddItem("Melee");
        _attackTypeChoices.Add(AttackType.Melee);

        if (attackerUnit.RangedWeaponRof > 0)
        {
            _attackTypeOption.AddItem("Ranged");
            _attackTypeChoices.Add(AttackType.Ranged);
        }

        try
        {
            _attackerSpells = await _apiSettings.Client.GetAsync<List<Dto.Spell>>(
                $"/api/units/{attackerUnit.Id}/spells");
        }
        catch (Exception)
        {
            _attackerSpells = new List<Dto.Spell>();
        }

        if (_attackerSpells.Count > 0)
        {
            _attackTypeOption.AddItem("Spell");
            _attackTypeChoices.Add(AttackType.Spell);
        }

        _attackTypeOption.Selected = 0;
        UpdateAttackTypeUi(AttackType.Melee);
    }

    private void UpdateAttackTypeUi(AttackType attackType)
    {
        _spellRow.Visible = attackType == AttackType.Spell;
        _weaponInfoLabel.Visible = attackType == AttackType.Ranged;

        if (attackType == AttackType.Spell)
        {
            _spellOption.Clear();
            foreach (var spell in _attackerSpells)
            {
                _spellOption.AddItem($"{spell.Name} (RNG {spell.Range}, POW {spell.Pow})");
            }

            _spellOption.Selected = _attackerSpells.Count > 0 ? 0 : -1;
        }
        else if (attackType == AttackType.Ranged &&
                 _attackerOption.Selected >= 0 &&
                 _board.GetModelDetails(_attackerIds[_attackerOption.Selected]) is { } details)
        {
            var unit = details.Unit;
            var aoeText = unit.RangedWeaponAoe > 0 ? $"{unit.RangedWeaponAoe}\"" : "-";
            var sprayText = unit.RangedWeaponIsSpray ? "SP " : string.Empty;
            _weaponInfoLabel.Text =
                $"{sprayText}RNG {unit.RangedWeaponRange}\"  ROF {unit.RangedWeaponRof}  AOE {aoeText}  " +
                $"POW {unit.RangedWeaponPow}/{unit.RangedWeaponBlastPow}";
        }
    }

    private async Task DoAttack()
    {
        _resultLabel.Text = string.Empty;
        _rollColumnButton.Visible = false;
        _statusLabel.Text = string.Empty;

        if (_attackerOption.Selected < 0 || _targetOption.Selected < 0 || _attackTypeOption.Selected < 0)
        {
            _statusLabel.Text = "Select an attacker, target, and attack type.";
            return;
        }

        var attackerId = _attackerIds[_attackerOption.Selected];
        var targetId = _targetIds[_targetOption.Selected];
        if (attackerId == targetId)
        {
            _statusLabel.Text = "Attacker and target must be different models.";
            return;
        }

        var attackerDetails = _board.GetModelDetails(attackerId);
        var targetDetails = _board.GetModelDetails(targetId);
        if (attackerDetails is null || targetDetails is null)
        {
            _statusLabel.Text = "Could not resolve attacker/target.";
            return;
        }

        var (attackerModel, attackerUnit, _) = attackerDetails.Value;
        var (targetModel, targetUnit, _) = targetDetails.Value;
        var attackType = _attackTypeChoices[_attackTypeOption.Selected];

        Dto.Spell? spell = null;
        double rangeInches;
        var eventType = Dto.CombatLogEventType.MeleeAttack;
        var verb = "attacks";

        switch (attackType)
        {
            case AttackType.Melee:
                rangeInches = attackerUnit.MeleeWeaponRange;
                eventType = Dto.CombatLogEventType.MeleeAttack;
                verb = "melee attacks";
                break;
            case AttackType.Ranged:
                rangeInches = attackerUnit.RangedWeaponRange;
                eventType = Dto.CombatLogEventType.RangedAttack;
                verb = "shoots";
                break;
            case AttackType.Spell:
                if (_spellOption.Selected < 0 || _spellOption.Selected >= _attackerSpells.Count)
                {
                    _statusLabel.Text = "Select a spell.";
                    return;
                }

                spell = _attackerSpells[_spellOption.Selected];
                rangeInches = spell.Range;
                eventType = Dto.CombatLogEventType.SpellCast;
                verb = $"casts {spell.Name} on";
                break;
            default:
                return;
        }

        var distanceInches = RangeCheck.DistanceInInches(attackerModel, targetModel);
        if (distanceInches > rangeInches)
        {
            _statusLabel.Text = $"Out of range ({distanceInches:0.0}\" > {rangeInches}\").";
            return;
        }

        var fromWorld = new Vector2((float)attackerModel.X, (float)attackerModel.Y);
        var toWorld = new Vector2((float)targetModel.X, (float)targetModel.Y);
        if (!LineOfSight.HasClearLineOfSight(fromWorld, toWorld, _board.Terrain))
        {
            _statusLabel.Text = "No line of sight to target.";
            return;
        }

        _attackButton.Disabled = true;
        try
        {
            var result = attackType switch
            {
                AttackType.Melee => AttackResolver.ResolveMeleeAttack(attackerUnit, targetUnit),
                AttackType.Ranged => AttackResolver.ResolveRangedAttack(attackerUnit, targetUnit),
                AttackType.Spell => AttackResolver.ResolveSpellAttack(attackerUnit, spell!, targetUnit),
                _ => throw new InvalidOperationException()
            };

            var toHitStat = attackType switch
            {
                AttackType.Melee => attackerUnit.MeleeAttack,
                AttackType.Ranged => attackerUnit.RangedAttack,
                AttackType.Spell => attackerUnit.ArcaneAttack,
                _ => 0
            };

            if (!result.Hit)
            {
                _resultLabel.Text =
                    $"To-hit: {result.ToHitRoll.Die1}+{result.ToHitRoll.Die2}={result.ToHitRoll.Total} " +
                    $"+ {toHitStat} vs DEF {targetUnit.Defense} -> Miss";

                await LogAttack(attackerId, targetId, false, 0, verb, eventType);
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
                _rollColumnButton.Visible = true;
                _statusLabel.Text = "Hit a warjack/warbeast - roll for damage column.";

                await LogAttack(attackerId, targetId, true, result.DamageDealt, verb, eventType);
            }
            else
            {
                await ApplyFlatDamage(targetModel, targetUnit, result.DamageDealt, result.InflictsKnockdown);
                await LogAttack(attackerId, targetId, true, result.DamageDealt, verb, eventType);
            }
        }
        finally
        {
            _attackButton.Disabled = false;
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

            _rollColumnButton.Visible = false;
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

    private async Task LogAttack(
        Guid attackerId, Guid targetId, bool hit, int damage, string verb, Dto.CombatLogEventType eventType)
    {
        if (_board.Session is null)
        {
            return;
        }

        var description = hit
            ? $"{_board.GetModelLabel(attackerId)} {verb} {_board.GetModelLabel(targetId)} for {damage} damage."
            : $"{_board.GetModelLabel(attackerId)} {verb} {_board.GetModelLabel(targetId)} and misses.";

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
}
