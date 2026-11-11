using System;
using WarmachineClient.Api.Dto;

namespace WarmachineClient.Rules;

public record AttackResult(DiceRollResult ToHitRoll, bool Hit, DiceRollResult? DamageRoll, int DamageDealt);

public static class AttackResolver
{
    public static AttackResult ResolveMeleeAttack(UnitDefinition attackerUnit, UnitDefinition targetUnit)
    {
        var toHitRoll = DiceRoller.Roll2d6();
        var hit = toHitRoll.Total + attackerUnit.MeleeAttack >= targetUnit.Defense;

        if (!hit)
        {
            return new AttackResult(toHitRoll, false, null, 0);
        }

        var damageRoll = DiceRoller.Roll2d6();
        var damageDealt = Math.Max(0, damageRoll.Total + attackerUnit.MeleeWeaponPow - targetUnit.Armor);

        return new AttackResult(toHitRoll, true, damageRoll, damageDealt);
    }
}
