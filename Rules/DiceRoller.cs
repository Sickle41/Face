using System;

namespace WarmachineClient.Rules;

public record DiceRollResult(int Die1, int Die2, int Total, bool IsDoubles);

public static class DiceRoller
{
    private static readonly Random Random = new();

    public static DiceRollResult Roll2d6()
    {
        var die1 = Random.Next(1, 7);
        var die2 = Random.Next(1, 7);
        return new DiceRollResult(die1, die2, die1 + die2, die1 == die2);
    }
}
