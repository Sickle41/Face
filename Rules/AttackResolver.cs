using System;
using WarmachineClient.Api.Dto;

namespace WarmachineClient.Rules;

public record AttackResult(DiceRollResult ToHitRoll, bool Hit, DiceRollResult? DamageRoll, int DamageDealt)
{
    /// <summary>
    /// A doubles damage roll knocks the target down - a simplified stand-in for the real rulebook's
    /// per-weapon critical-effect text, which isn't modeled as structured data yet.
    /// </summary>
    public bool InflictsKnockdown => Hit && (DamageRoll?.IsDoubles ?? false);
}

public static class AttackResolver
{
    public static AttackResult ResolveMeleeAttack(UnitDefinition attackerUnit, UnitDefinition targetUnit)
    {
        return Resolve(attackerUnit.MeleeAttack, attackerUnit.MeleeWeaponPow, targetUnit);
    }

    public static AttackResult ResolveRangedAttack(UnitDefinition attackerUnit, UnitDefinition targetUnit)
    {
        return Resolve(attackerUnit.RangedAttack, attackerUnit.RangedWeaponPow, targetUnit);
    }

    public static AttackResult ResolveSpellAttack(UnitDefinition attackerUnit, Spell spell, UnitDefinition targetUnit)
    {
        return Resolve(attackerUnit.ArcaneAttack, spell.Pow, targetUnit);
    }

    private static AttackResult Resolve(int toHitStat, int pow, UnitDefinition targetUnit)
    {
        var toHitRoll = DiceRoller.Roll2d6();
        var hit = toHitRoll.Total + toHitStat >= targetUnit.Defense;

        if (!hit)
        {
            return new AttackResult(toHitRoll, false, null, 0);
        }

        var damageRoll = DiceRoller.Roll2d6();
        var damageDealt = Math.Max(0, damageRoll.Total + pow - targetUnit.Armor);

        return new AttackResult(toHitRoll, true, damageRoll, damageDealt);
    }
}
