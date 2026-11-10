using System;
using System.Collections.Generic;

namespace WarmachineClient.Api.Dto;

public enum StatusEffect
{
    KnockedDown,
    Stationary,
    Disrupted,
    Slammed,
    Rooted
}

public class DamageColumnState
{
    public string Name { get; set; } = string.Empty;
    public int BoxesFilled { get; set; }
}

public class ModelInstance
{
    public Guid Id { get; set; }
    public Guid GameSessionId { get; set; }
    public Guid ArmyEntryId { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Facing { get; set; }
    public int DamageTaken { get; set; }
    public bool IsDestroyed { get; set; }
    public List<StatusEffect> StatusEffects { get; set; } = new();
    public List<DamageColumnState> DamageGrid { get; set; } = new();
    public bool HasActivated { get; set; }
    public bool HasUsedFeat { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
