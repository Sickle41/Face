namespace WarmachineClient.Api.Dto;

public class ArmyPointsSummary
{
    public int TotalPoints { get; set; }
    public int PointLimit { get; set; }
    public int RemainingPoints { get; set; }
    public bool IsOverLimit { get; set; }
}
