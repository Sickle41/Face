namespace WarmachineClient.Rules;

public static class Units
{
    public const float MmPerInch = 25.4f;

    public static double ToInches(double mm) => mm / MmPerInch;

    public static double ToMm(double inches) => inches * MmPerInch;
}
