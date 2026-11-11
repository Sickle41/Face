using System.Collections.Generic;

namespace WarmachineClient.Api.Dto;

public class GameSessionParticipantSummary
{
    public System.Guid ArmyId { get; set; }
    public string ArmyName { get; set; } = string.Empty;
    public int ModelCount { get; set; }
    public int ModelsDestroyed { get; set; }
    public int TotalDamageTaken { get; set; }
    public int VictoryPoints { get; set; }
}

public class GameSessionSummary
{
    public SessionStatus Status { get; set; }
    public int CurrentRound { get; set; }
    public string? ScenarioName { get; set; }
    public List<GameSessionParticipantSummary> Participants { get; set; } = new();
    public Dictionary<CombatLogEventType, int> LogEntryCountsByType { get; set; } = new();
}
