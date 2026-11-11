using System;
using WarmachineClient.Api.Dto;

namespace WarmachineClient.Rules;

public static class RangeCheck
{
    public static double DistanceInInches(ModelInstance a, ModelInstance b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var distanceMm = Math.Sqrt(dx * dx + dy * dy);
        return Units.ToInches(distanceMm);
    }
}
