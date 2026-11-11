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
    private OptionButton _attackerOption = null!;
    private OptionButton _targetOption = null!;
    private Button _meleeAttackButton = null!;
    private Label _resultLabel = null!;
    private Button _rollColumnButton = null!;
    private Label _statusLabel = null!;

    private Board _board = null!;
    private ApiSettings _apiSettings = null!;

    private readonly List<Guid> _attackerIds = new();
    private readonly List<Guid> _targetIds = new();

    private Dto.ModelInstance? _pendingTargetModel;
    private Dto.UnitDefinition? _pendingTargetUnit;
    private int _pendingDamageAmount;

    public override void _Ready()
    {
        _attackerOption = GetNode<OptionButton>("Panel/VBoxContainer/AttackerRow/AttackerOption");
        _targetOption = GetNode<OptionButton>("Panel/VBoxContainer/TargetRow/TargetOption");
        _meleeAttackButton = GetNode<Button>("Panel/VBoxContainer/MeleeAttackButton");
        _resultLabel = GetNode<Label>("Panel/VBoxContainer/ResultLabel");
        _rollColumnButton = GetNode<Button>("Panel/VBoxContainer/RollColumnButton");
        _statusLabel = GetNode<Label>("Panel/VBoxContainer/StatusLabel");

        _apiSettings = GetNode<ApiSettings>("/root/ApiSettings");
        _board = GetParent<Board>();

        _rollColumnButton.Visible = false;
        _meleeAttackButton.Pressed += () => _ = DoMeleeAttack();
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
    }

    private async Task DoMeleeAttack()
    {
        _resultLabel.Text = string.Empty;
        _rollColumnButton.Visible = false;
        _statusLabel.Text = string.Empty;

        if (_attackerOption.Selected < 0 || _targetOption.Selected < 0)
        {
            _statusLabel.Text = "Select an attacker and a target.";
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

        var distanceInches = RangeCheck.DistanceInInches(attackerModel, targetModel);
        if (distanceInches > attackerUnit.MeleeWeaponRange)
        {
            _statusLabel.Text =
                $"Out of melee range ({distanceInches:0.0}\" > {attackerUnit.MeleeWeaponRange}\").";
            return;
        }

        var fromWorld = new Vector2((float)attackerModel.X, (float)attackerModel.Y);
        var toWorld = new Vector2((float)targetModel.X, (float)targetModel.Y);
        if (!LineOfSight.HasClearLineOfSight(fromWorld, toWorld, _board.Terrain))
        {
            _statusLabel.Text = "No line of sight to target.";
            return;
        }

        _meleeAttackButton.Disabled = true;
        try
        {
            var result = AttackResolver.ResolveMeleeAttack(attackerUnit, targetUnit);

            if (!result.Hit)
            {
                _resultLabel.Text =
                    $"To-hit: {result.ToHitRoll.Die1}+{result.ToHitRoll.Die2}={result.ToHitRoll.Total} " +
                    $"+ MAT {attackerUnit.MeleeAttack} vs DEF {targetUnit.Defense} -> Miss";

                await LogAttack(attackerId, targetId, result.Hit, 0, "misses");
                return;
            }

            _resultLabel.Text =
                $"To-hit: {result.ToHitRoll.Total} vs DEF {targetUnit.Defense} -> Hit. " +
                $"Damage: {result.DamageRoll!.Die1}+{result.DamageRoll.Die2}={result.DamageRoll.Total} " +
                $"+ POW {attackerUnit.MeleeWeaponPow} - ARM {targetUnit.Armor} = {result.DamageDealt}";

            if (targetUnit.DamageColumns.Count > 0)
            {
                _pendingTargetModel = targetModel;
                _pendingTargetUnit = targetUnit;
                _pendingDamageAmount = result.DamageDealt;
                _rollColumnButton.Visible = true;
                _statusLabel.Text = "Hit a warjack/warbeast - roll for damage column.";

                await LogAttack(attackerId, targetId, result.Hit, result.DamageDealt, "hits");
            }
            else
            {
                await ApplyFlatDamage(targetModel, targetUnit, result.DamageDealt);
                await LogAttack(attackerId, targetId, result.Hit, result.DamageDealt, "hits");
            }
        }
        finally
        {
            _meleeAttackButton.Disabled = false;
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

            while (grid.Count <= chosenIndex)
            {
                grid.Add(new Dto.DamageColumnState { Name = template[grid.Count].Name, BoxesFilled = 0 });
            }

            var column = grid[chosenIndex];
            column.BoxesFilled = Math.Min(template[chosenIndex].TotalBoxes, column.BoxesFilled + _pendingDamageAmount);

            var allDestroyed = grid.Zip(template, (g, t) => g.BoxesFilled >= t.TotalBoxes).All(x => x);

            await _apiSettings.Client.PatchAsync($"/api/models/{_pendingTargetModel.Id}/damage-grid", grid);

            if (allDestroyed)
            {
                await _apiSettings.Client.PatchAsync($"/api/models/{_pendingTargetModel.Id}/damage", new
                {
                    damageTaken = _pendingTargetModel.DamageTaken,
                    isDestroyed = true,
                    statusEffects = _pendingTargetModel.StatusEffects
                });
            }

            _statusLabel.Text = $"Rolled column: {template[chosenIndex].Name} " +
                                 $"({column.BoxesFilled}/{template[chosenIndex].TotalBoxes})" +
                                 (allDestroyed ? " - DESTROYED" : string.Empty);

            _rollColumnButton.Visible = false;
            _pendingTargetModel = null;
            _pendingTargetUnit = null;

            await _board.RefreshModels();
        }
        finally
        {
            _rollColumnButton.Disabled = false;
        }
    }

    private async Task ApplyFlatDamage(Dto.ModelInstance targetModel, Dto.UnitDefinition targetUnit, int damage)
    {
        var newDamage = targetModel.DamageTaken + damage;
        var destroyed = newDamage >= targetUnit.DamageCapacity;

        await _apiSettings.Client.PatchAsync($"/api/models/{targetModel.Id}/damage", new
        {
            damageTaken = newDamage,
            isDestroyed = destroyed,
            statusEffects = targetModel.StatusEffects
        });

        if (destroyed)
        {
            _statusLabel.Text = "Target destroyed.";
        }

        await _board.RefreshModels();
    }

    private async Task LogAttack(Guid attackerId, Guid targetId, bool hit, int damage, string verb)
    {
        if (_board.Session is null)
        {
            return;
        }

        var description = hit
            ? $"{_board.GetModelLabel(attackerId)} {verb} {_board.GetModelLabel(targetId)} for {damage} damage."
            : $"{_board.GetModelLabel(attackerId)} {verb} {_board.GetModelLabel(targetId)}.";

        try
        {
            await _apiSettings.Client.PostAsync<Dto.CombatLogEntry>($"/api/sessions/{_board.Session.Id}/log", new
            {
                round = _board.Session.CurrentRound,
                eventType = Dto.CombatLogEventType.MeleeAttack,
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
