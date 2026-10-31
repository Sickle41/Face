using System;

namespace WarmachineClient.Api.Dto;

public class GameParticipant
{
    public Guid Id { get; set; }
    public Guid GameSessionId { get; set; }
    public Guid ArmyId { get; set; }
    public int TurnOrder { get; set; }
    public int VictoryPoints { get; set; }
}
