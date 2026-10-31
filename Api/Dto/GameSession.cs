using System;

namespace WarmachineClient.Api.Dto;

public enum SessionStatus
{
    Setup,
    InProgress,
    Completed
}

public class GameSession
{
    public Guid Id { get; set; }
    public Guid MapId { get; set; }
    public Guid? ScenarioId { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Setup;
    public int CurrentRound { get; set; } = 1;
    public Guid? ActiveParticipantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid ConcurrencyToken { get; set; }
}
