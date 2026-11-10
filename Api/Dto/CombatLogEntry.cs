using System;

namespace WarmachineClient.Api.Dto;

public enum CombatLogEventType
{
    Movement,
    MeleeAttack,
    RangedAttack,
    SpellCast,
    Damage,
    Destroyed,
    Custom
}

public class CombatLogEntry
{
    public Guid Id { get; set; }
    public Guid GameSessionId { get; set; }
    public int Round { get; set; }
    public CombatLogEventType EventType { get; set; }
    public Guid? ActorModelInstanceId { get; set; }
    public Guid? TargetModelInstanceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
